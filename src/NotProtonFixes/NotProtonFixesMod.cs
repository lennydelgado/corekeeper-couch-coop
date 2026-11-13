using System;
using HarmonyLib;
using PugMod;
using Shared;
using UnityEngine;

namespace NotProtonFixes
{
    /// <summary>
    /// Fixes needed to play Core Keeper on macOS through NotProton (CrossOver's Wine, patched).
    /// Every fix defaults to "auto": active only when Wine is detected, so it does nothing on Windows.
    /// Also works inside the dedicated server (file fixes only).
    ///
    /// Settings (official PugMod.API.Config): .../Core Keeper/Steam/&lt;id&gt;/mods/NotProtonFixes/*.json
    /// </summary>
    public class NotProtonFixesMod : IMod
    {
        public const string Name = "NotProtonFixes";
        public const string Version = "1.0.0";
        internal const string Tag = "[NotProtonFixes]";

        /// <summary>"auto" (under Wine only) | "on" | "off"</summary>
        internal static string CloudMode = "auto", WriteMode = "auto";
        internal static bool KeepRunningInBackground = true;

        private static Harmony _harmony;

        internal static bool SafeWrites => Active(WriteMode);
        internal static bool SkipCloud => Active(CloudMode);
        private static bool Active(string mode) => mode == "on" || (mode == "auto" && Runtime.IsWine);

        public void EarlyInit()
        {
            bool server = Runtime.IsDedicatedServer;
            Debug.Log($"{Tag} v{Version} - wine={Runtime.IsWine} ({Runtime.WineVersion}) server={server}");

            // Patch first: EarlyInit runs before FilesystemManager.Init and the cloud sync, and reading the
            // settings right below already goes through the file fixes.
            if (_harmony == null)
            {
                _harmony = new Harmony(Name);
                Patch(typeof(SafeWritePatch));
                Patch(typeof(FileExistsPatch));
                if (!server)
                {
                    Patch(typeof(CloudSyncDownPatch));
                    Patch(typeof(CloudSyncUpPatch));
                }
            }

            LoadSettings();
            if (!server && KeepRunningInBackground && Runtime.IsWine) Application.runInBackground = true;
            Debug.Log($"{Tag} safeWrites={SafeWrites} skipCloud={SkipCloud} runInBackground={Application.runInBackground}");
        }

        private static void Patch(Type t)
        {
            try { _harmony.CreateClassProcessor(t).Patch(); }
            catch (Exception e) { Debug.LogError($"{Tag} could not apply {t.Name}: {e.Message}"); }
        }

        private static void LoadSettings()
        {
            try
            {
                var cfg = API.Config;
                if (cfg == null) return;
                WriteMode = cfg.Register(Name, "Files", "Write saves without File.Replace/Delete (broken under NotProton). auto | on | off", "safeWrites", "auto").Value;
                CloudMode = cfg.Register(Name, "Files", "Skip the game's Steam Cloud sync (crashes the first load under NotProton). auto | on | off", "skipCloudSync", "auto").Value;
                KeepRunningInBackground = cfg.Register(Name, "Window", "Keep the game running when macOS takes the focus away", "runInBackground", true).Value;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} settings unreadable, using defaults: {e.Message}");
            }
        }

        public void Init() { }
        public void Shutdown() { }
        public void ModObjectLoaded(UnityEngine.Object obj) { }
        public void Update() { }
    }
}
