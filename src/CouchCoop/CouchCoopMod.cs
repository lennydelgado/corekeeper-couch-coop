using System;
using HarmonyLib;
using PugMod;
using Shared;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Couch Co-op: local split-screen co-op for Core Keeper.
    ///  - P1 / P2 controller picker (in-game hub, controller or keyboard);
    ///  - second game window + official dedicated server running locally: P2 plays their own character;
    ///  - windows snapped to screen halves, widened render (no black bars), configurable pixel scale;
    ///  - "Join With IP" pre-filled, P1 and P2 admins of the local server.
    /// </summary>
    public class CouchCoopMod : IMod
    {
        public const string Name = "CouchCoop";
        public const string Version = "1.0.0";
        internal const string Tag = "[CouchCoop]";

        private static Harmony _harmony;
        private static GameObject _host;

        public void EarlyInit()
        {
            if (Runtime.IsDedicatedServer)
            {
                Debug.Log($"{Tag} dedicated server: nothing to do (client-side mod)");
                return;
            }

            Settings.Load();
            PadStore.Load();

            if (_harmony == null)
            {
                _harmony = new Harmony(Name);
                foreach (var t in new[]
                {
                    typeof(AssignJoysticksPatch), typeof(TouchpadMapsPatch), typeof(ControllerConnectedPatch),
                    typeof(ControllerDisconnectedPatch), typeof(PixelWidthPatch), typeof(PixelHeightPatch),
                    typeof(BilinearQuadPatch), typeof(JoinMenuPatch)
                })
                {
                    try { _harmony.CreateClassProcessor(t).Patch(); }
                    catch (Exception e) { Debug.LogError($"{Tag} could not apply {t.Name}: {e.Message}"); }
                }
            }

            // The hub exists from the loading screen on (it places the P2 window, etc.).
            if (_host == null)
            {
                _host = new GameObject("CouchCoop");
                UnityEngine.Object.DontDestroyOnLoad(_host);
                _host.AddComponent<UI.Hub>();
            }

            Diagnostics.Init();
            Duo.EarlyInit();
            Debug.Log($"{Tag} v{Version} ready - dedicated server installed={Duo.ServerInstalled}");
        }

        public void Init() { }
        public void Shutdown() { }
        public void ModObjectLoaded(UnityEngine.Object obj) { }
        public void Update() { }
    }
}
