using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Shared
{
    /// <summary>
    /// File helpers that work under NotProton's Wine, where (observed) File.Exists returns true for missing
    /// files and File.Delete / File.Replace / RemoveDirectory fail, while create / write / read / rename work.
    /// Everywhere else they behave like the usual .NET calls.
    /// </summary>
    internal static class SafeFile
    {
        private const uint MOVEFILE_REPLACE_EXISTING = 0x1, MOVEFILE_WRITE_THROUGH = 0x8;

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileExW(string from, string to, uint flags);

        private static string Win(string p) => p.Replace('/', '\\');

        /// <summary>Reliable existence check: try to open the file.</summary>
        public static bool Exists(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { }
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (UnauthorizedAccessException) { return !Directory.Exists(path) && File.Exists(path); }
            catch (IOException) { return true; } // exists but locked
        }

        /// <summary>Atomic rename that replaces the target, without deleting it first.</summary>
        public static bool MoveReplace(string from, string to, out int error)
        {
            try
            {
                bool ok = MoveFileExW(Win(from), Win(to), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH);
                error = ok ? 0 : Marshal.GetLastWin32Error();
                return ok;
            }
            catch (Exception)
            {
                error = -1; // no kernel32 (should not happen: the game is Windows-only)
                return false;
            }
        }

        /// <summary>Writes a whole file safely (temp file + atomic replace, falls back to a direct write).</summary>
        public static void WriteAllBytes(string path, byte[] data)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }
            if (MoveReplace(tmp, path, out _)) return;
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }
        }

        public static void WriteAllText(string path, string text) =>
            WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(text));

        public static string ReadAllTextOrNull(string path)
        {
            try { return Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }
    }
}
