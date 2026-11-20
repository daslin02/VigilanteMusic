using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using IOFile = System.IO.File;     
using TagFile = TagLib.File;
using VigilanteMusic.DataBase;

namespace VigilanteMusic.SoundParser
{
    public class MusicInfo
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public uint? Year { get; set; }
        public string Genre { get; set; }
        public TimeSpan Duration { get; set; }
        public string FilePath { get; set; }
        public byte[] Picture { get; set; }

        public override string ToString() => Describe();

        /// <summary>Overridable description used by ToString.</summary>
        protected virtual string Describe()
        {
            return $"{Artist} - {Title} ({Album}, {Year})";
        }
    }
    /// <summary>
    /// MusicInfo extended with the columns that only exist in the database.
    /// </summary>
    public class MusicNetInfo : MusicInfo
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public long FileSizeBytes { get; set; }
        public string FileHash { get; set; }
        public int? TrackNumber { get; set; }
        public int? Bitrate { get; set; }
        public int? SampleRate { get; set; }
        public int? Channels { get; set; }
        public string StorageMode { get; set; }
        public DateTime? ModifiedUtc { get; set; }

        /// <summary>True when the audio bytes live inside the database.</summary>
        public bool IsBlob => string.Equals(StorageMode, "blob", StringComparison.OrdinalIgnoreCase);

        protected virtual string DescribeNet()
        {
            return $"[{Id}] {Artist} - {Title} ({Album}, {Year}) {(FileSizeBytes / 1048576.0):F2} MB";
        }

        public override string ToString() => DescribeNet();
    }

     public class Music
    {
        private MusicInfo _info;
        private bool _infoLoaded;

        /// <summary>
        /// Path of the source file. Empty for tracks that exist only in the database.
        /// </summary>
        public string FilePath { get; protected set; }

        /// <summary>
        /// Metadata of this track. Read from the file on first access and cached afterwards.
        /// </summary>
        public virtual MusicInfo Info
        {
            get
            {
                if (!_infoLoaded)
                {
                    _info = GetInfo(FilePath);
                    _infoLoaded = true;
                }
                return _info;
            }
            protected set
            {
                _info = value;
                _infoLoaded = true;
            }
        }

        public string Title => Info?.Title;
        public string Artist => Info?.Artist;
        public string Album => Info?.Album;
        public TimeSpan Duration => Info != null ? Info.Duration : TimeSpan.Zero;

        /// <summary>True once metadata has been read successfully.</summary>
        public bool HasInfo => Info != null;

        /// <summary>
        /// Creates a track bound to a local file. Tags are not read until Info is requested.
        /// </summary>
        public Music(string filePath)
        {
            FilePath = filePath;
        }

        /// <summary>
        /// Creates a track from already known metadata, skipping any file access.
        /// Used by MusicNet when the row comes from the database.
        /// </summary>
        protected Music(MusicInfo info)
        {
            _info = info;
            _infoLoaded = true;
            FilePath = info?.FilePath;
        }

        /// <summary>
        /// Forces a re-read of the tags on the next Info access.
        /// </summary>
        public void RefreshInfo()
        {
            _infoLoaded = false;
            _info = null;
        }

        /// <summary>
        /// Extracts metadata from an audio file using TagLib#
        /// </summary>
        /// <param name="filePath">Path of Audio file</param>
        /// <returns>A MusicInfo object containing metadata, or null if an error occurs</returns>
        public static MusicInfo GetInfo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                return null;
            }

            try
            {
                using (var file = TagLib.File.Create(filePath))
                {
                    if (file == null || file.Tag == null)
                        return null;

                    var info = new MusicInfo
                    {
                        FilePath = filePath,
                        Title = file.Tag.Title ?? System.IO.Path.GetFileNameWithoutExtension(filePath),
                        Artist = file.Tag.FirstPerformer ?? "Unknown Artist",
                        Album = file.Tag.Album ?? "Unknown Album",

                        Year = file.Tag.Year > 0 ? (uint?)file.Tag.Year : null,

                        Genre = file.Tag.FirstGenre,
                        Duration = file.Properties.Duration
                    };

                    if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                    {
                        info.Picture = file.Tag.Pictures[0].Data.Data;
                    }

                    return info;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading tags from {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts metadata from a list of files
        /// </summary>
        /// <param name="filePaths">List of file paths</param>
        /// <returns>Dictionary path -> MusicInfo</returns>
        public static Dictionary<string, MusicInfo> GetBatchInfo(List<string> filePaths)
        {
            var result = new Dictionary<string, MusicInfo>();

            foreach (var path in filePaths)
            {
                var info = GetInfo(path);
                if (info != null)
                {
                    result[path] = info;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the local path of the track. Base implementation only resolves an existing file.
        /// Overridden by MusicNet to materialize a blob from the database first.
        /// </summary>
        public virtual string Load()
        {
            if (string.IsNullOrEmpty(FilePath)) return null;
            return System.IO.File.Exists(FilePath) ? FilePath : null;
        }

        /// <summary>
        /// Saves the audio into the given folder. Empty in the base class - local files need no download.
        /// </summary>
        /// <param name="saveDirectory">Folder where the file must be written</param>
        /// <returns>Full path of the saved file, or null on failure</returns>
        public virtual string Download(string saveDirectory)
        {
            return null;
        }

        public override string ToString()
        {
            return Info != null ? Info.ToString() : System.IO.Path.GetFileName(FilePath);
        }
    }
    public class MusicNet : Music
    {
        private readonly DBReader _reader;

        public int Id { get; private set; }

        /// <summary>Path of the materialized file, null until Load or Download succeeds.</summary>
        public string LocalPath => base.FilePath;

        public bool IsLoaded => !string.IsNullOrEmpty(LocalPath) && IOFile.Exists(LocalPath);

        /// <summary>
        /// Creates a track backed by the database. Metadata is loaded lazily on first access to Info.
        /// </summary>
        public MusicNet(int id, DBReader reader = null) : base((MusicInfo)null)
        {
            Id = id;
            _reader = reader ?? new DBReader();
        }

        /// <summary>
        /// Reads metadata from dbo.Tracks. No audio bytes and no disk access.
        /// </summary>
        public static MusicNetInfo GetInfo(int id, DBReader reader = null)
        {
            var db = reader ?? new DBReader();
            TrackRow row = db.GetById(id, withCover: true);
            return row == null ? null : Map(row);
        }

        /// <summary>Same lookup, but by the original path recorded during import.</summary>
        public static MusicNetInfo GetInfo(string filePath, DBReader reader = null)
        {
            var db = reader ?? new DBReader();
            TrackRow row = db.GetByPath(filePath, withCover: true);
            return row == null ? null : Map(row);
        }

        /// <summary>Metadata of every track in the table.</summary>
        public static List<MusicNetInfo> GetAllInfo(DBReader reader = null, bool withCover = false)
        {
            var db = reader ?? new DBReader();
            var result = new List<MusicNetInfo>();
            foreach (TrackRow row in db.GetAllTracks(withCover))
                result.Add(Map(row));
            return result;
        }

        /// <summary>
        /// Overrides base.Info to fetch data from the database instead of a local file.
        /// The result is cached in the base class so the query runs only once.
        /// </summary>
        public override MusicInfo Info
        {
            get
            {
                if (base.Info == null)
                {
                    base.Info = GetInfo(Id, _reader);
                }
                return base.Info;
            }
            protected set { base.Info = value; }
        }

        /// <summary>
        /// Pulls the audio blob out of the database into the shared cache and returns its path.
        /// For rows stored in path mode the original file path is returned as is.
        /// </summary>
        public override string Load()
        {
            if (IsLoaded) return LocalPath;

            string path = NetMusicController.Instance.Acquire(Id, _reader);
            if (!string.IsNullOrEmpty(path))
            {
                // Update the base FilePath so other parts of the system see the real location
                base.FilePath = path;
            }
            return path;
        }

        /// <summary>
        /// Writes the audio into saveDirectory and returns the resulting full path.
        /// The file is kept outside the cache, so eviction never removes it.
        /// </summary>
        public override string Download(string saveDirectory)
        {
            if (string.IsNullOrWhiteSpace(saveDirectory)) return null;

            // Ensure we have metadata to know the filename
            if (Info == null) Info = GetInfo(Id, _reader);
            if (Info == null) return null;

            Directory.CreateDirectory(saveDirectory);

            string name = SanitizeFileName(((MusicNetInfo)Info).FileName ?? (Info.Title + (Info is MusicNetInfo mni ? mni.Extension : ".mp3")));
            string target = Path.Combine(saveDirectory, name);

            // path mode: copy the original file if it is reachable
            if (!((MusicNetInfo)Info).IsBlob)
            {
                if (string.IsNullOrEmpty(Info.FilePath) || !IOFile.Exists(Info.FilePath)) return null;
                IOFile.Copy(Info.FilePath, target, true);
                return target;
            }

            string temp = target + ".part";
            try
            {
                using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (!_reader.WriteAudioTo(Id, fs)) return null;
                }

                if (IOFile.Exists(target)) IOFile.Delete(target);
                IOFile.Move(temp, target);
                return target;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusicNet] Download failed for {Id}: {ex.Message}");
                TryDelete(temp);
                return null;
            }
        }

        /// <summary>Drops the cached file reference held by this instance.</summary>
        public void Unload()
        {
            if (!string.IsNullOrEmpty(LocalPath))
            {
                NetMusicController.Instance.Release(LocalPath);
                // Reset FilePath to null or original DB path if needed, 
                // but usually keeping it null forces re-load next time
                base.FilePath = null;
            }
        }

        // ---------- helpers ----------

        private static MusicNetInfo Map(TrackRow row)
        {
            return new MusicNetInfo
            {
                Id = row.Id,
                FilePath = row.FilePath,
                FileName = row.FileName,
                Extension = row.Extension,
                FileSizeBytes = row.FileSizeBytes,
                FileHash = row.FileHash,
                Title = row.Title ?? Path.GetFileNameWithoutExtension(row.FileName),
                Artist = row.Artist ?? "Unknown Artist",
                Album = row.Album ?? "Unknown Album",
                Year = row.Year.HasValue && row.Year.Value > 0 ? (uint?)row.Year.Value : null,
                Genre = row.Genre,
                Duration = row.DurationMs.HasValue ? TimeSpan.FromMilliseconds(row.DurationMs.Value) : TimeSpan.Zero,
                Picture = row.CoverArt,
                TrackNumber = row.TrackNumber,
                Bitrate = row.Bitrate,
                SampleRate = row.SampleRate,
                Channels = row.Channels,
                StorageMode = row.StorageMode,
                ModifiedUtc = row.ModifiedUtc
            };
        }

        internal static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "track.bin";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        internal static void TryDelete(string path)
        {
            try { if (IOFile.Exists(path)) IOFile.Delete(path); }
            catch { /* file may still be locked by the player */ }
        }
    }
}