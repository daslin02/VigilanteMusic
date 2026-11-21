using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VigilanteMusic.DataBase;

namespace VigilanteMusic.SoundParser
{
    /// <summary>
    /// Keeps downloaded database tracks in a local cache folder.
    /// Once the number of cached files exceeds the limit, the least recently used
    /// unreferenced files are deleted.
    /// </summary>
    public sealed class NetMusicController
    {
        private static readonly Lazy<NetMusicController> _instance =
            new Lazy<NetMusicController>(() => new NetMusicController());

        public static NetMusicController Instance => _instance.Value;

        private class CacheEntry
        {
            public int TrackId;
            public string Path;
            public long Size;
            public DateTime LastAccessUtc;
            public int RefCount;
        }

        private readonly Dictionary<string, CacheEntry> _entries =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly object _sync = new object();

        /// <summary>Maximum number of files kept on disk.</summary>
        public int MaxCachedTracks { get; set; } = 10;

        /// <summary>Optional byte budget, 0 disables it.</summary>
        public long MaxCacheBytes { get; set; } = 0;

        public string CacheDirectory { get; private set; }

        /// <summary>Raised after a file has been evicted, so the UI can refresh.</summary>
        public event Action<string> TrackEvicted;

        private NetMusicController()
        {
            CacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VigilanteMusic", "Cache");

            Directory.CreateDirectory(CacheDirectory);
            AdoptExistingFiles();
        }

        public int CachedCount
        {
            get { lock (_sync) return _entries.Count; }
        }

        public long CachedBytes
        {
            get { lock (_sync) return _entries.Values.Sum(e => e.Size); }
        }

        /// <summary>
        /// Returns a local path for the track, downloading it when needed.
        /// The caller should pair this with Release once playback finishes.
        /// </summary>
        public string Acquire(int trackId, DBReader reader = null)
        {
            lock (_sync)
            {
                CacheEntry existing = _entries.Values.FirstOrDefault(e => e.TrackId == trackId);
                if (existing != null && File.Exists(existing.Path))
                {
                    existing.RefCount++;
                    existing.LastAccessUtc = DateTime.UtcNow;
                    return existing.Path;
                }

                var db = reader ?? new DBReader();
                TrackRow row = db.GetById(trackId);
                if (row == null) return null;

                string path;

                if (row.IsBlob)
                {
                    path = MaterializeBlob(db, row);
                    if (path == null) return null;
                }
                else
                {
                    // path mode: nothing to download, use the original location
                    if (string.IsNullOrEmpty(row.FilePath) || !File.Exists(row.FilePath)) return null;
                    path = row.FilePath;
                }

                var entry = new CacheEntry
                {
                    TrackId = trackId,
                    Path = path,
                    Size = SafeLength(path),
                    LastAccessUtc = DateTime.UtcNow,
                    RefCount = 1
                };

                _entries[path] = entry;
                EvictIfNeeded();
                return path;
            }
        }

        /// <summary>
        /// Marks a cached file as no longer in use. It becomes eligible for eviction.
        /// </summary>
        public void Release(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            lock (_sync)
            {
                CacheEntry entry;
                if (!_entries.TryGetValue(path, out entry)) return;
                if (entry.RefCount > 0) entry.RefCount--;
                EvictIfNeeded();
            }
        }

        /// <summary>Deletes every unreferenced cached file.</summary>
        public void Clear()
        {
            lock (_sync)
            {
                foreach (var entry in _entries.Values.ToList())
                {
                    if (entry.RefCount > 0) continue;
                    RemoveEntry(entry);
                }
            }
        }

        /// <summary>Snapshot of the current cache contents, useful for a debug view.</summary>
        public List<string> GetCachedPaths()
        {
            lock (_sync) return _entries.Values.Select(e => e.Path).ToList();
        }

        // ---------- internals ----------

        private string MaterializeBlob(DBReader db, TrackRow row)
        {
            string name = MusicNet.SanitizeFileName(row.FileName);
            string target = Path.Combine(CacheDirectory, row.Id + "_" + name);
            string temp = target + ".part";

            try
            {
                using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (!db.WriteAudioTo(row.Id, fs)) return null;
                }

                if (File.Exists(target)) File.Delete(target);
                File.Move(temp, target);
                return target;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NetMusicController] Download failed: {ex.Message}");
                MusicNet.TryDelete(temp);
                return null;
            }
        }

        /// <summary>
        /// Removes least recently used entries while the limits are exceeded.
        /// Files currently referenced by a player are skipped.
        /// </summary>
        private void EvictIfNeeded()
        {
            while (true)
            {
                bool overCount = MaxCachedTracks > 0 && _entries.Count > MaxCachedTracks;
                bool overBytes = MaxCacheBytes > 0 && _entries.Values.Sum(e => e.Size) > MaxCacheBytes;
                if (!overCount && !overBytes) break;

                CacheEntry victim = _entries.Values
                    .Where(e => e.RefCount <= 0)
                    .OrderBy(e => e.LastAccessUtc)
                    .FirstOrDefault();

                if (victim == null) break;   
                RemoveEntry(victim);
            }
        }

        private void RemoveEntry(CacheEntry entry)
        {
            _entries.Remove(entry.Path);

            // never delete files outside the cache folder (path-mode originals)
            if (IsInsideCache(entry.Path))
                MusicNet.TryDelete(entry.Path);

            TrackEvicted?.Invoke(entry.Path);
        }

        private bool IsInsideCache(string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                string root = Path.GetFullPath(CacheDirectory)
                                  .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>Registers leftovers from a previous run so they count towards the limit.</summary>
        private void AdoptExistingFiles()
        {
            try
            {
                foreach (string file in Directory.GetFiles(CacheDirectory))
                {
                    if (file.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                    {
                        MusicNet.TryDelete(file);
                        continue;
                    }

                    _entries[file] = new CacheEntry
                    {
                        TrackId = ParseIdFromName(Path.GetFileName(file)),
                        Path = file,
                        Size = SafeLength(file),
                        LastAccessUtc = File.GetLastAccessTimeUtc(file),
                        RefCount = 0
                    };
                }
                EvictIfNeeded();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NetMusicController] Adopt failed: {ex.Message}");
            }
        }

        private static int ParseIdFromName(string fileName)
        {
            int underscore = fileName.IndexOf('_');
            if (underscore <= 0) return -1;
            int id;
            return int.TryParse(fileName.Substring(0, underscore), out id) ? id : -1;
        }

        private static long SafeLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }
    }
}