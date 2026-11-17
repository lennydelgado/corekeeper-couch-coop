using System;
using System.IO;
using Shared;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Rotating world backups in persistentDataPath/CouchCoop/Backups/&lt;kind&gt;/1..5 (the oldest slot is overwritten).
    /// Each kind rotates on its own, so the many session-start backups never push out a rarer safety copy.
    /// Files are overwritten, never deleted: deleting fails under NotProton.
    /// Each slot holds world.gzip, world.worldinfo (when there is one) and about.txt (what, when, from where).
    /// To restore by hand: game closed, copy world.gzip / world.worldinfo back over the paths in about.txt.
    /// </summary>
    internal static class WorldBackup
    {
        public const int Slots = 5;

        /// <summary>Server world, each time split screen starts.</summary>
        public const string Session = "session";
        /// <summary>A world about to be replaced by a copy (bring back to solo).</summary>
        public const string BeforeReplace = "before-replace";

        public static string Root => Duo.ShareDir + "/Backups";
        private static string LastPath(string kind) => $"{Root}/{kind}/last.txt";

        /// <summary>Copies a world into the next slot of its kind. Returns the slot folder, or null if nothing was copied.</summary>
        public static string Save(string kind, string label, string worldFile, string infoFile)
        {
            try
            {
                if (!SafeFile.Exists(worldFile)) return null;
                int slot = NextSlot(kind);
                string dir = $"{Root}/{kind}/{slot}";
                Directory.CreateDirectory(dir);
                SafeFile.WriteAllBytes(dir + "/world.gzip", File.ReadAllBytes(worldFile));
                bool info = !string.IsNullOrEmpty(infoFile) && SafeFile.Exists(infoFile);
                if (info) SafeFile.WriteAllBytes(dir + "/world.worldinfo", File.ReadAllBytes(infoFile));
                SafeFile.WriteAllText(dir + "/about.txt",
                    $"{label}\nsaved: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nworld: {worldFile}\nworldinfo: {(info ? infoFile : "(none, ignore any world.worldinfo in this folder)")}\n");
                SafeFile.WriteAllText(LastPath(kind), slot.ToString());
                Debug.Log($"{CouchCoopMod.Tag} backup {kind} {slot}/{Slots}: {label} ({worldFile})");
                return dir;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{CouchCoopMod.Tag} backup failed ({label}): {e.Message}");
                return null;
            }
        }

        private static int NextSlot(string kind)
        {
            int.TryParse(SafeFile.ReadAllTextOrNull(LastPath(kind))?.Trim(), out int last);
            return last >= 1 && last < Slots ? last + 1 : 1;
        }
    }
}
