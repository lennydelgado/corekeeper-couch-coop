using System;
using System.IO;
using HarmonyLib;
using Pug.Platform;
using Shared;
using UnityEngine;

namespace NotProtonFixes
{
    /// <summary>
    /// Under NotProton, StandaloneFilesystem.Write fails (File.Exists lies, File.Delete/Replace fail):
    /// saves stay as *.pugtmp and are never finalized.
    /// Replacement: temp file, backup copy, then MoveFileEx(REPLACE_EXISTING), with a direct write as fallback.
    /// On any unexpected failure we log the step and let the game's own code run.
    /// </summary>
    [HarmonyPatch(typeof(StandaloneFilesystem), nameof(StandaloneFilesystem.Write),
        new[] { typeof(string), typeof(string), typeof(byte[]) })]
    internal static class SafeWritePatch
    {
        private static int _okLogs;

        private static bool Prefix(StandaloneFilesystem __instance, string path, byte[] data)
        {
            if (!NotProtonFixesMod.SafeWrites) return true;

            string step = "rel2abs";
            string abs = null;
            try
            {
                abs = __instance.Rel2Abs(path);
                string tmp = abs + ".pugtmp";

                step = "mkdir";
                string dir = Path.GetDirectoryName(abs);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                step = "write tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true);
                }

                step = "backup";
                if (SafeFile.Exists(abs))
                {
                    try { File.Copy(abs, abs + ".pugbackup", true); } catch { /* backup is optional */ }
                }

                step = "move tmp";
                if (!SafeFile.MoveReplace(tmp, abs, out int err))
                {
                    Debug.LogWarning($"{NotProtonFixesMod.Tag} MoveFileEx failed ({err}) for {path}, writing directly");
                    step = "direct write";
                    using (var fs = new FileStream(abs, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fs.Write(data, 0, data.Length);
                        fs.Flush(true);
                    }
                }

                if (_okLogs++ < 3) Debug.Log($"{NotProtonFixesMod.Tag} saved {path} ({data.Length} bytes)");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"{NotProtonFixesMod.Tag} safe write failed at step '{step}' for {abs ?? path}: "
                               + $"{e.GetType().Name} {e.Message} (0x{e.HResult:X8}), falling back to the game's code");
                return true;
            }
        }
    }

    /// <summary>The game's FileExists is File.Exists, which lies under NotProton: test by opening the file.</summary>
    [HarmonyPatch(typeof(StandaloneFilesystem), nameof(StandaloneFilesystem.FileExists))]
    internal static class FileExistsPatch
    {
        private static bool Prefix(StandaloneFilesystem __instance, string path, ref bool __result)
        {
            if (!NotProtonFixesMod.SafeWrites) return true;
            try
            {
                __result = SafeFile.Exists(__instance.Rel2Abs(path));
                return false;
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>The game's own Steam Cloud sync goes through NotProton's ported lsteamclient and breaks the first load.</summary>
    [HarmonyPatch(typeof(SteamPlatform), nameof(SteamPlatform.CloudSyncDown))]
    internal static class CloudSyncDownPatch
    {
        private static bool Prefix()
        {
            if (!NotProtonFixesMod.SkipCloud) return true;
            Debug.Log($"{NotProtonFixesMod.Tag} Steam Cloud sync skipped (local saves only)");
            return false;
        }
    }

    [HarmonyPatch(typeof(SteamPlatform), nameof(SteamPlatform.CloudSyncUp))]
    internal static class CloudSyncUpPatch
    {
        private static bool Prefix() => !NotProtonFixesMod.SkipCloud;
    }
}
