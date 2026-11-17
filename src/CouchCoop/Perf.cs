using System;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CouchCoop
{
    /// <summary>
    /// Split screen runs three copies of Unity on one machine (P1, P2 and the dedicated server).
    ///
    /// Frame pacing: every frame, Manager.Update drops a window WITHOUT focus to 30 fps (V-Sync off, target 30)
    /// and the focused one follows V-Sync (120 fps on a 120 Hz TV). In split screen one window never has the
    /// focus, so that player always played at 30 fps with 2 queued frames: ~100 ms of extra input lag.
    /// Right after Manager.Update we give both windows the same pacing: V-Sync off, capped at
    /// <see cref="Settings.FpsLimit"/> (60), 1 queued frame (lowest latency). Windows are composited by macOS
    /// (not exclusive full screen), so there is no tearing without V-Sync.
    ///
    /// Freeze log: frames longer than 250 ms are logged with a timestamp (to match P1, P2 and the server logs)
    /// and the garbage collections they contain, plus a summary every minute ([perf] lines).
    /// </summary>
    internal static class Perf
    {
        private const float HitchSeconds = 0.25f;

        private static float _summaryAt = -1f, _worst;
        private static int _frames, _slow, _hitches, _gc0, _gc2;
        private static int _lastGc0 = -1, _lastGc2 = -1;
        private static bool _loggedCap;

        private static string Now => DateTime.Now.ToString("HH:mm:ss.f");

        public static void Tick()
        {
            if (!Duo.Active) return;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;

            int g0 = SafeGc(0), g2 = SafeGc(2);
            if (_lastGc0 < 0) { _lastGc0 = g0; _lastGc2 = g2; }
            int dg0 = g0 - _lastGc0, dg2 = g2 - _lastGc2;
            _lastGc0 = g0; _lastGc2 = g2;
            _gc0 += dg0; _gc2 += dg2;

            _frames++;
            if (dt > 0.05f) _slow++;
            if (dt > _worst) _worst = dt;
            if (dt > HitchSeconds && Time.frameCount > 10)
            {
                _hitches++;
                Debug.Log($"{CouchCoopMod.Tag} [perf] {Now} freeze {dt * 1000f:0} ms (gc +{dg0}, full gc +{dg2}, focused={Application.isFocused}, scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name})");
            }

            if (_summaryAt < 0f) _summaryAt = now + 60f;
            if (now >= _summaryAt)
            {
                Debug.Log($"{CouchCoopMod.Tag} [perf] {Now} last 60 s: {_frames / 60f:0} fps avg, {_slow} frames > 50 ms, worst {_worst * 1000f:0} ms, {_hitches} freezes, gc {_gc0} (full {_gc2}), vsync={QualitySettings.vSyncCount} cap={Application.targetFrameRate} queued={QualitySettings.maxQueuedFrames} focused={Application.isFocused}");
                _summaryAt = now + 60f;
                _frames = _slow = _hitches = _gc0 = _gc2 = 0;
                _worst = 0f;
            }
        }

        private static int SafeGc(int gen)
        {
            try { return GC.CollectionCount(gen); } catch { return 0; }
        }

        /// <summary>Called right after the game's own pacing (Manager.Update), every frame.</summary>
        public static void ApplyFramePacing()
        {
            if (!Duo.Active) return;
            int cap = Settings.FpsLimit;
            if (cap <= 0) return; // "game setting"
            if (QualitySettings.vSyncCount != 0) QualitySettings.vSyncCount = 0;
            if (Application.targetFrameRate != cap) Application.targetFrameRate = cap;
            if (QualitySettings.maxQueuedFrames != 1) QualitySettings.maxQueuedFrames = 1;
            if (!_loggedCap)
            {
                _loggedCap = true;
                Debug.Log($"{CouchCoopMod.Tag} [perf] pacing: {cap} fps in both windows (focused or not), V-Sync off, 1 queued frame");
            }
        }

        /// <summary>The local server gets a higher priority than the two game windows: it drives both.</summary>
        public static void BoostServer(Process server)
        {
            if (server == null) return;
            try
            {
                server.PriorityClass = ProcessPriorityClass.AboveNormal;
                Debug.Log($"{CouchCoopMod.Tag} [perf] local server priority: {server.PriorityClass}");
            }
            catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} server priority: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(Manager), "Update")]
    internal static class FramePacingPatch
    {
        private static void Postfix() => Perf.ApplyFramePacing();
    }
}
