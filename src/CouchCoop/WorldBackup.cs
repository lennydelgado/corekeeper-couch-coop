using System;
using System.IO;
using Shared;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Rotating world backups in persistentDataPath/CouchCoop/Backups/1..5 (the oldest slot is overwritten).
    /// Files are overwritten, never deleted: deleting fails under NotProton.
    /// Each slot holds world.gzip, world.worldinfo (when there is one) and about.txt (what, when, from where).
    /// To restore by hand: game closed, copy world.gzip / world.worldinfo back over the paths in about.txt.
    /// </summary>
    internal static class WorldBackup
    {
        public const int Slots = 5;

        public static string Root => Duo.ShareDir + "/Backups";
        private static string LastPath => Root + "/last.txt";

        /// <summary>Copies a world into the next slot. Returns the slot folder, or null if nothing was copied.</summary>
        public static string Save(string label, string worldFile, string infoFile)
        {
            try
            {
                if (!SafeFile.Exists(worldFile)) return null;
                int slot = NextSlot();
                string dir = $"{Root}/{slot}";
                Directory.CreateDirectory(dir);
                SafeFile.WriteAllBytes(dir + "/world.gzip", File.ReadAllBytes(worldFile));
                bool info = !string.IsNullOrEmpty(infoFile) && SafeFile.Exists(infoFile);
                if (info) SafeFile.WriteAllBytes(dir + "/world.worldinfo", File.ReadAllBytes(infoFile));
                SafeFile.WriteAllText(dir + "/about.txt",
                    $"{label}\nsaved: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nworld: {worldFile}\nworldinfo: {(info ? infoFile : "(none, ignore any world.worldinfo in this folder)")}\n");
                SafeFile.WriteAllText(LastPath, slot.ToString());
                Debug.Log($"{CouchCoopMod.Tag} backup {slot}/{Slots}: {label} ({worldFile})");
                return dir;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{CouchCoopMod.Tag} backup failed ({label}): {e.Message}");
                return null;
            }
        }

        private static int NextSlot()
        {
            int last = 0;
            int.TryParse(SafeFile.ReadAllTextOrNull(LastPath)?.Trim(), out last);
            return last >= 1 && last < Slots ? last + 1 : 1;
        }
    }
}
