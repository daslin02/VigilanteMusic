using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using VigilanteMusic.DataBase;

namespace VigilanteMusic.DataBase
{
    /// <summary>
    /// Raw row from dbo.Tracks. Metadata only unless a blob column was requested.
    /// </summary>
    public class TrackRow
    {
        public int Id { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public long FileSizeBytes { get; set; }
        public string FileHash { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public int? Year { get; set; }
        public int? TrackNumber { get; set; }
        public int? DurationMs { get; set; }
        public int? Bitrate { get; set; }
        public int? SampleRate { get; set; }
        public int? Channels { get; set; }
        public string StorageMode { get; set; }
        public DateTime? ModifiedUtc { get; set; }
        public byte[] CoverArt { get; set; }
        public byte[] AudioData { get; set; }

        public bool IsBlob => string.Equals(StorageMode, "blob", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads tracks from the database. Never touches audio files on disk.
    /// </summary>
    public class DBReader
    {
        private readonly DBConnector _connector;
        private readonly bool _ownsConnector;

        private const string MetaColumns =
            "Id, FilePath, FileName, Extension, FileSizeBytes, FileHash, " +
            "Title, Artist, Album, Genre, [Year], TrackNumber, DurationMs, " +
            "Bitrate, SampleRate, Channels, StorageMode, ModifiedUtc";

        public DBReader(DBConnector connector = null)
        {
            if (connector != null)
            {
                _connector = connector;
                _ownsConnector = false;
            }
            else
            {
                _connector = new DBConnector();
                _ownsConnector = true;
            }
        }

        /// <summary>
        /// Loads metadata of every track. Audio and cover blobs are not fetched.
        /// </summary>
        public List<TrackRow> GetAllTracks(bool withCover = false)
        {
            string sql = "SELECT " + MetaColumns + (withCover ? ", CoverArt" : "") +
                         " FROM dbo.Tracks ORDER BY Artist, Title;";

            var list = new List<TrackRow>();

            using (var conn = _connector.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    while (reader.Read())
                    {
                        TrackRow row = MapMeta(reader);
                        if (withCover) row.CoverArt = ReadBlob(reader, 18);
                        list.Add(row);
                    }
                }
            }
            return list;
        }

        /// <summary>Loads one track by primary key.</summary>
        public TrackRow GetById(int id, bool withCover = false)
        {
            string sql = "SELECT " + MetaColumns + (withCover ? ", CoverArt" : "") +
                         " FROM dbo.Tracks WHERE Id = @id;";

            using (var conn = _connector.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    if (!reader.Read()) return null;
                    TrackRow row = MapMeta(reader);
                    if (withCover) row.CoverArt = ReadBlob(reader, 18);
                    return row;
                }
            }
        }

        /// <summary>Loads one track by the original file path stored during import.</summary>
        public TrackRow GetByPath(string filePath, bool withCover = false)
        {
            string sql = "SELECT " + MetaColumns + (withCover ? ", CoverArt" : "") +
                         " FROM dbo.Tracks WHERE FilePath = @path;";

            using (var conn = _connector.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@path", filePath ?? "");

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    if (!reader.Read()) return null;
                    TrackRow row = MapMeta(reader);
                    if (withCover) row.CoverArt = ReadBlob(reader, 18);
                    return row;
                }
            }
        }

        /// <summary>Case-insensitive search over title, artist and album.</summary>
        public List<TrackRow> Search(string query)
        {
            string sql = "SELECT " + MetaColumns + " FROM dbo.Tracks " +
                         "WHERE Title LIKE @q OR Artist LIKE @q OR Album LIKE @q " +
                         "ORDER BY Artist, Title;";

            var list = new List<TrackRow>();

            using (var conn = _connector.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@q", "%" + (query ?? "") + "%");

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    while (reader.Read()) list.Add(MapMeta(reader));
                }
            }
            return list;
        }

        /// <summary>
        /// Streams the audio blob into a destination stream.
        /// Returns false when the row holds no bytes (path storage mode).
        /// </summary>
        public bool WriteAudioTo(int id, Stream destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            using (var conn = _connector.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT AudioData FROM dbo.Tracks WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@id", id);

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    if (!reader.Read() || reader.IsDBNull(0)) return false;

                    using (var source = reader.GetStream(0))
                    {
                        source.CopyTo(destination, 256 * 1024);
                    }
                    return true;
                }
            }
        }

        /// <summary>Reads only the cover art column.</summary>
        public byte[] GetCover(int id)
        {
            using (var conn = _connector.OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT CoverArt FROM dbo.Tracks WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@id", id);

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    if (!reader.Read()) return null;
                    return ReadBlob(reader, 0);
                }
            }
        }

        /// <summary>Number of rows in the table.</summary>
        public int Count()
        {
            object result = _connector.Scalar("SELECT COUNT(*) FROM dbo.Tracks;");
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static TrackRow MapMeta(IDataRecord r)
        {
            return new TrackRow
            {
                Id = r.GetInt32(0),
                FilePath = Str(r, 1),
                FileName = Str(r, 2),
                Extension = Str(r, 3),
                FileSizeBytes = r.IsDBNull(4) ? 0 : r.GetInt64(4),
                FileHash = Str(r, 5),
                Title = Str(r, 6),
                Artist = Str(r, 7),
                Album = Str(r, 8),
                Genre = Str(r, 9),
                Year = Int(r, 10),
                TrackNumber = Int(r, 11),
                DurationMs = Int(r, 12),
                Bitrate = Int(r, 13),
                SampleRate = Int(r, 14),
                Channels = Int(r, 15),
                StorageMode = Str(r, 16),
                ModifiedUtc = r.IsDBNull(17) ? (DateTime?)null : r.GetDateTime(17)
            };
        }

        private static string Str(IDataRecord r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

        private static int? Int(IDataRecord r, int i) => r.IsDBNull(i) ? (int?)null : r.GetInt32(i);

        private static byte[] ReadBlob(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;
            using (var src = reader.GetStream(ordinal))
            using (var ms = new MemoryStream())
            {
                src.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}