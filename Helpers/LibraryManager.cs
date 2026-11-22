using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VigilanteMusic.Config;
using VigilanteMusic.SoundParser;

namespace VigilanteMusic.Helpers
{
    /// <summary>
    /// Central library state: scan folders, saved playlists and the database tracks.
    /// The UI reads lists from here and calls the mutation methods below.
    /// </summary>
    public class LibraryManager
    {
        /// <summary>Name of the virtual playlist that shows everything.</summary>
        public const string AllPlaylistName = "ALL";

        private readonly List<Music> _localTracks = new List<Music>();
        private readonly List<Music> _dbTracks = new List<Music>();
        private readonly Dictionary<string, List<Music>> _savedPlaylists =
            new Dictionary<string, List<Music>>(StringComparer.OrdinalIgnoreCase);

        private string _currentPlaylist = AllPlaylistName;

        /// <summary>Raised whenever the visible track list changes.</summary>
        public event Action LibraryChanged;

        /// <summary>Raised whenever the set of playlist names changes.</summary>
        public event Action PlaylistListChanged;

        public IReadOnlyList<Music> LocalTracks => _localTracks;
        public IReadOnlyList<Music> DbTracks => _dbTracks;
        public string CurrentPlaylist => _currentPlaylist;

        // ------------------------------------------------------------------ //
        // Loading
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Rescans every folder listed in musicfolders.json.
        /// Existing Music instances are reused so playback references stay valid.
        /// </summary>
        public void ReloadLocalTracks()
        {
            var found = MusicScanner.GetAllMusic();
            var known = new Dictionary<string, Music>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in _localTracks)
                if (!string.IsNullOrEmpty(t.FilePath) && !known.ContainsKey(t.FilePath))
                    known[t.FilePath] = t;

            _localTracks.Clear();

            foreach (string path in found)
            {
                Music existing;
                _localTracks.Add(known.TryGetValue(path, out existing) ? existing : new Music(path));
            }

            LibraryChanged?.Invoke();
        }

        /// <summary>Replaces the database backed tracks with a fresh set.</summary>
        public void SetDatabaseTracks(IEnumerable<Music> tracks)
        {
            _dbTracks.Clear();
            if (tracks != null) _dbTracks.AddRange(tracks.Where(t => t != null));
            LibraryChanged?.Invoke();
        }

        /// <summary>Loads every saved playlist from disk into memory.</summary>
        public void ReloadSavedPlaylists()
        {
            _savedPlaylists.Clear();

            var map = PlayListGeneric.GetAllPlayListFiles();
            foreach (var pair in map)
                _savedPlaylists[pair.Key] = PlayListGeneric.BuildPlayListByName(map, pair.Key);

            PlaylistListChanged?.Invoke();
        }

        /// <summary>
        /// All playlist names shown in the left panel: ALL plus every saved file.
        /// </summary>
        public List<string> GetPlaylistNames()
        {
            var names = new List<string> { AllPlaylistName };
            names.AddRange(_savedPlaylists.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
            return names;
        }

        // ------------------------------------------------------------------ //
        // Switching
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the tracks of the requested playlist and makes it current.
        /// ALL merges local files and database rows.
        /// </summary>
        public List<Music> SwitchTo(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName)) return new List<Music>();

            _currentPlaylist = playlistName;
            return GetTracksOf(playlistName);
        }

        /// <summary>Reads the tracks of a playlist without changing the current selection.</summary>
        public List<Music> GetTracksOf(string playlistName)
        {
            if (string.Equals(playlistName, AllPlaylistName, StringComparison.OrdinalIgnoreCase))
            {
                var merged = new List<Music>(_localTracks);
                merged.AddRange(_dbTracks);
                return merged;
            }

            List<Music> saved;
            return _savedPlaylists.TryGetValue(playlistName, out saved)
                ? new List<Music>(saved)
                : new List<Music>();
        }

        /// <summary>True when the playlist is a real file that can be edited.</summary>
        public bool IsEditable(string playlistName)
        {
            return !string.IsNullOrWhiteSpace(playlistName)
                && !string.Equals(playlistName, AllPlaylistName, StringComparison.OrdinalIgnoreCase)
                && _savedPlaylists.ContainsKey(playlistName);
        }

        // ------------------------------------------------------------------ //
        // Scan folders
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Registers a new folder for scanning, saves the config and rescans.
        /// Returns the number of tracks that appeared.
        /// </summary>
        public int AddScanFolder(string folderPath)
        {
            string normalized = PathHelper.Normalize(folderPath);
            if (normalized == null || !Directory.Exists(normalized)) return 0;

            var store = MusicFolderStore.Load();

            bool alreadyListed = store.Folders.Any(f => PathHelper.AreEqual(f, normalized));
            if (!alreadyListed)
            {
                store.Folders.Add(normalized);
                store.Save();
            }

            int before = _localTracks.Count;
            ReloadLocalTracks();
            return _localTracks.Count - before;
        }

        /// <summary>Removes a folder from the scan list and drops its tracks.</summary>
        public bool RemoveScanFolder(string folderPath)
        {
            string normalized = PathHelper.Normalize(folderPath);
            if (normalized == null) return false;

            var store = MusicFolderStore.Load();
            int removed = store.Folders.RemoveAll(f => PathHelper.AreEqual(f, normalized));
            if (removed == 0) return false;

            store.Save();

            // drop tracks that lived inside the removed folder
            _localTracks.RemoveAll(t => PathHelper.IsSubFolderOf(t.FilePath, normalized));

            LibraryChanged?.Invoke();
            return true;
        }

        /// <summary>Folders currently registered for scanning.</summary>
        public List<string> GetScanFolders() => MusicFolderStore.Load().Folders;

        // ------------------------------------------------------------------ //
        // Playlist management
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Creates an empty playlist file and selects it.
        /// Returns false when the name is reserved or already taken.
        /// </summary>
        public bool CreatePlaylist(string name, List<Music> initialTracks = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (string.Equals(name, AllPlaylistName, StringComparison.OrdinalIgnoreCase)) return false;
            if (_savedPlaylists.ContainsKey(name)) return false;

            var tracks = initialTracks ?? new List<Music>();
            PlayListGeneric.CreatePlayListFile(tracks, name);

            _savedPlaylists[name] = new List<Music>(tracks);
            PlaylistListChanged?.Invoke();
            return true;
        }

        /// <summary>Deletes a playlist file. ALL cannot be deleted.</summary>
        public bool DeletePlaylist(string name)
        {
            if (!IsEditable(name)) return false;
            if (!PlayListGeneric.RemovePlayList(name)) return false;

            _savedPlaylists.Remove(name);

            if (string.Equals(_currentPlaylist, name, StringComparison.OrdinalIgnoreCase))
                _currentPlaylist = AllPlaylistName;

            PlaylistListChanged?.Invoke();
            LibraryChanged?.Invoke();
            return true;
        }

        /// <summary>Writes the in-memory state of a playlist back to its JSON file.</summary>
        public bool SavePlaylist(string name)
        {
            List<Music> tracks;
            if (!_savedPlaylists.TryGetValue(name, out tracks)) return false;

            PlayListGeneric.UpdatePlayList(tracks, name);
            return true;
        }

        // ------------------------------------------------------------------ //
        // Track management inside a playlist
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Adds tracks to a saved playlist, skipping duplicates.
        /// Returns the number of tracks actually added.
        /// </summary>
        public int AddTracks(string playlistName, IEnumerable<Music> tracks)
        {
            if (!IsEditable(playlistName) || tracks == null) return 0;

            var target = _savedPlaylists[playlistName];
            int added = 0;

            foreach (var track in tracks)
            {
                if (track == null) continue;
                if (ContainsTrack(target, track)) continue;

                target.Add(track);
                added++;
            }

            if (added > 0)
            {
                SavePlaylist(playlistName);
                LibraryChanged?.Invoke();
            }
            return added;
        }

        /// <summary>Adds a single local file to a playlist.</summary>
        public bool AddFile(string playlistName, string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
            return AddTracks(playlistName, new[] { new Music(filePath) }) > 0;
        }

        /// <summary>
        /// Removes a track by index from a saved playlist.
        /// The file itself is never deleted from disk.
        /// </summary>
        public bool RemoveTrackAt(string playlistName, int index)
        {
            if (!IsEditable(playlistName)) return false;

            var target = _savedPlaylists[playlistName];
            if (index < 0 || index >= target.Count) return false;

            target.RemoveAt(index);
            SavePlaylist(playlistName);
            LibraryChanged?.Invoke();
            return true;
        }

        /// <summary>Removes several indexes at once, highest first so they stay valid.</summary>
        public int RemoveTracksAt(string playlistName, IEnumerable<int> indexes)
        {
            if (!IsEditable(playlistName) || indexes == null) return 0;

            var target = _savedPlaylists[playlistName];
            int removed = 0;

            foreach (int index in indexes.OrderByDescending(i => i))
            {
                if (index < 0 || index >= target.Count) continue;
                target.RemoveAt(index);
                removed++;
            }

            if (removed > 0)
            {
                SavePlaylist(playlistName);
                LibraryChanged?.Invoke();
            }
            return removed;
        }

        /// <summary>Empties a saved playlist without deleting the file.</summary>
        public bool ClearPlaylist(string playlistName)
        {
            if (!IsEditable(playlistName)) return false;

            _savedPlaylists[playlistName].Clear();
            SavePlaylist(playlistName);
            LibraryChanged?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ //
        // Helpers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Compares tracks by identity: database id for network tracks, path for local ones.
        /// Reference equality is not enough because the same song may come from two sources.
        /// </summary>
        private static bool ContainsTrack(List<Music> list, Music candidate)
        {
            foreach (var item in list)
            {
                if (ReferenceEquals(item, candidate)) return true;

                var netA = item as MusicNet;
                var netB = candidate as MusicNet;

                if (netA != null && netB != null)
                {
                    if (netA.Id == netB.Id) return true;
                    continue;
                }

                if (netA == null && netB == null && PathHelper.AreEqual(item.FilePath, candidate.FilePath))
                    return true;
            }
            return false;
        }
    }
}