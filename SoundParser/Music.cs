using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;

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

        public override string ToString()
        {
            return $"{Artist} - {Title} ({Album}, {Year})";
        }
    }

    public class Music
    {
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
    }
}