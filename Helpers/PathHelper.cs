using System;
using System.IO;

namespace VigilanteMusic.Helpers
{
    public static class PathHelper
    {
        /// <summary>Converts a path to a standardized format: a full path using '\' as the separator, with no trailing slash.</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                string full = Path.GetFullPath(path.Trim().Trim('"'));
                full = full.Replace('/', '\\');
                full = full.TrimEnd('\\');
                // fix root disk path text
                if (full.Length == 2 && full[1] == ':') full += "\\";

                return full;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Comparison of paths, ignoring case and slashes.</summary>
        public static bool AreEqual(string a, string b)
        {
            string na = Normalize(a);
            string nb = Normalize(b);
            if (na == null || nb == null) return false;
            return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Determines whether 'child' is a subfolder of 'parent' (or the same folder).</summary>
        public static bool IsSubFolderOf(string child, string parent)
        {
            string c = Normalize(child);
            string p = Normalize(parent);
            if (c == null || p == null) return false;

            if (!p.EndsWith("\\")) p += "\\";
            if (!c.EndsWith("\\")) c += "\\";

            return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
        }

        public static bool FolderExists(string path)
        {
            string n = Normalize(path);
            return n != null && Directory.Exists(n);
        }
    }
}