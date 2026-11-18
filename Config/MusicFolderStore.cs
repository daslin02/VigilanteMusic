using System;
using System.Collections.Generic;
using System.IO;
using VigilanteMusic.Helpers;

namespace VigilanteMusic.Config
{
    /// <summary>
    /// Root config object stored in musicfolders.json.
    /// Holds the list of folders that must be scanned for audio files.
    /// </summary>
    public class MusicFolderStore
    {
        /// <summary>Config format version, useful for future migrations.</summary>
        public int Version { get; set; } = 1;

        /// <summary>File extensions treated as audio. Lowercase, with leading dot.</summary>
        public List<string> Extensions { get; set; } = new List<string>
        {
            ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma"
        };

        /// <summary>Absolute paths of folders to scan.</summary>
        public List<string> Folders { get; set; } = new List<string>();

        // ---------- config location ----------

        public static string ConfigDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VigilanteMusic");

        public static string ConfigFilePath =>
            Path.Combine(ConfigDirectory, "musicfolders.json");

        // ---------- load / save ----------

        /// <summary>
        /// Loads the config from disk. Creates a default one if the file is missing or corrupted.
        /// </summary>
        public static MusicFolderStore Load()
        {
            var store = JsonHelper.LoadFromFile<MusicFolderStore>(ConfigFilePath);

            if (store == null)
            {
                store = CreateDefault();
                store.Save();
            }

            // guard against null collections after deserialization
            if (store.Folders == null) store.Folders = new List<string>();
            if (store.Extensions == null || store.Extensions.Count == 0)
                store.Extensions = CreateDefault().Extensions;

            return store;
        }

        public bool Save() => JsonHelper.SaveToFile(ConfigFilePath, this);

        private static MusicFolderStore CreateDefault()
        {
            var store = new MusicFolderStore();
            store.Folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            return store;
        }
    }
}