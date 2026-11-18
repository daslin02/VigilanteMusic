using System;
using System.Collections.Generic;
using System.IO;
using VigilanteMusic.Config;

namespace VigilanteMusic.Helpers
{
    /// <summary>
    /// Scans every folder registered in the config and collects audio file paths.
    /// </summary>
    public static class MusicScanner
    {
        /// <summary>
        /// Returns absolute paths of all audio files found in all registered folders.
        /// Duplicates are removed, result is sorted alphabetically.
        /// </summary>
        public static List<string> GetAllMusic()
        {
            return GetAllMusic(MusicFolderStore.Load());
        }

        /// <summary>
        /// Same scan, but with an explicitly provided config (no disk read).
        /// </summary>
        public static List<string> GetAllMusic(MusicFolderStore store)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (store == null || store.Folders == null)
                return new List<string>();

            // build extension lookup once
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string ext in store.Extensions)
            {
                if (string.IsNullOrWhiteSpace(ext)) continue;
                string e = ext.Trim();
                if (!e.StartsWith(".")) e = "." + e;
                extensions.Add(e);
            }

            foreach (string folder in store.Folders)
            {
                string root = PathHelper.Normalize(folder);
                if (root == null || !Directory.Exists(root)) continue;

                foreach (string file in EnumerateFilesSafe(root))
                {
                    string ext = Path.GetExtension(file);
                    if (string.IsNullOrEmpty(ext) || !extensions.Contains(ext)) continue;

                    result.Add(PathHelper.Normalize(file));
                }
            }

            var list = new List<string>(result);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>
        /// Walks the directory tree iteratively.
        /// Skips folders that cannot be accessed instead of aborting the whole scan,
        /// and ignores reparse points to avoid junction loops.
        /// </summary>
        private static IEnumerable<string> EnumerateFilesSafe(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string current = pending.Pop();

                string[] files;
                try
                {
                    files = Directory.GetFiles(current);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException
                                        || ex is DirectoryNotFoundException
                                        || ex is IOException)
                {
                    continue;
                }

                foreach (string file in files)
                    yield return file;

                string[] dirs;
                try
                {
                    dirs = Directory.GetDirectories(current);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException
                                        || ex is DirectoryNotFoundException
                                        || ex is IOException)
                {
                    continue;
                }

                foreach (string dir in dirs)
                {
                    try
                    {
                        FileAttributes attr = File.GetAttributes(dir);
                        if ((attr & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch
                    {
                        continue;
                    }

                    pending.Push(dir);
                }
            }
        }
    }
}