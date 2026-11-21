using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using VigilanteMusic.SoundParser;

namespace VigilanteMusic.Helpers
{
    /// <summary>
    /// Intermediate DTO for serializing Music tracks to JSON.
    /// Stores either a local file path or a Database ID.
    /// </summary>
    [DataContract]
    public class MusicOnFile
    {
        [DataMember(Name = "path")]
        public string PathToFile { get; set; }

        [DataMember(Name = "isNetwork")]
        public bool IsNetwork { get; set; }

        public MusicOnFile() { }

        public MusicOnFile(string path, bool isNetwork)
        {
            PathToFile = path;
            IsNetwork = isNetwork;
        }
    }

    /// <summary>
    /// Manages persistent playlists stored as JSON files.
    /// </summary>
    public static class PlayListGeneric
    {
        private static readonly string PlaylistsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VigilanteMusic",
            "Playlists");

        /// <summary>
        /// Creates a playlist file containing the given tracks.
        /// </summary>
        /// <param name="tracks">List of Music objects (local or network).</param>
        /// <param name="playlistName">Name of the playlist (used as filename).</param>
        public static void CreatePlayListFile(List<Music> tracks, string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName)) return;
            if (tracks == null) tracks = new List<Music>();

            Directory.CreateDirectory(PlaylistsDirectory);

            // Convert Music objects to serializable DTOs
            var dtos = new List<MusicOnFile>();
            foreach (var track in tracks)
            {
                if (track == null) continue;

                bool isNet = track is MusicNet;
                string path;
                if (!isNet) 
                {
                    path = track.FilePath;
                }
                else 
                {
                    path =  ((MusicNet)track).Id.ToString();
                }

                dtos.Add(new MusicOnFile(path, isNet));
            }

            string fileName = SanitizeFileName(playlistName) + ".json";
            string filePath = Path.Combine(PlaylistsDirectory, fileName);

            JsonHelper.SaveToFile(filePath, dtos);
        }

        /// <summary>
        /// Scans the playlist directory and returns all saved playlists.
        /// Key: Playlist Name, Value: List of serialized track data.
        /// </summary>
        public static Dictionary<string, List<MusicOnFile>> GetAllPlayListFiles()
        {
            var result = new Dictionary<string, List<MusicOnFile>>();

            if (!Directory.Exists(PlaylistsDirectory)) return result;

            foreach (string file in Directory.GetFiles(PlaylistsDirectory, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                var dtos = JsonHelper.LoadFromFile<List<MusicOnFile>>(file);

                if (dtos != null)
                {
                    result[name] = dtos;
                }
            }

            return result;
        }

        /// <summary>
        /// Reconstructs a list of Music objects from a specific playlist entry in the map.
        /// </summary>
        /// <param name="map">The map returned by GetAllPlayListFiles.</param>
        /// <param name="name">The name of the playlist to load.</param>
        /// <returns>List of reconstructed Music/MusicNet objects.</returns>
        public static List<Music> BuildPlayListByName(Dictionary<string, List<MusicOnFile>> map, string name)
        {
            if (map == null || !map.ContainsKey(name)) return new List<Music>();

            var dtos = map[name];
            var tracks = new List<Music>();

            foreach (var dto in dtos)
            {
                if (dto == null) continue;

                Music track;
                if (dto.IsNetwork)
                {
                    // Try to parse ID, if it fails skip
                    if (int.TryParse(dto.PathToFile, out int id))
                    {
                        track = new MusicNet(id);
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    // Local file
                    if (string.IsNullOrEmpty(dto.PathToFile) || !File.Exists(dto.PathToFile))
                    {
                        continue; // Skip missing files
                    }
                    track = new Music(dto.PathToFile);
                }

                tracks.Add(track);
            }

            return tracks;
        }

        /// <summary>
        /// Helper to clean playlist names for filenames.
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Untitled";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
        /// <summary>
        /// Updates an existing playlist file with a new list of tracks.
        /// If the playlist does not exist, it creates a new one.
        /// </summary>
        /// <param name="tracks">The updated list of Music objects.</param>
        /// <param name="playlistName">The name of the playlist to update.</param>
        public static void UpdatePlayList(List<Music> tracks, string playlistName)
        {
            // Reuse the creation logic as it overwrites the file if it exists (via SaveToFile atomic write)
            CreatePlayListFile(tracks, playlistName);
        }

        /// <summary>
        /// Deletes a specific playlist file from the storage directory.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to remove.</param>
        /// <returns>True if the file was found and deleted, false otherwise.</returns>
        public static bool RemovePlayList(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName)) return false;

            string fileName = SanitizeFileName(playlistName) + ".json";
            string filePath = Path.Combine(PlaylistsDirectory, fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayListGeneric] Failed to delete playlist: {ex.Message}");
            }

            return false;
        }
    }
}