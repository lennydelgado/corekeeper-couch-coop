using HarmonyLib;
using Pug.RP;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Black bars in split screen: the game renders in "game pixels" (480x270 at 16:9, width capped at 630 in
    /// MatchAspect mode), then letterboxes the final image to 16:9 (BilinearQuadAutoSizer).
    /// In split screen we pick a render size that fills the window EXACTLY with an integer scale
    /// (e.g. 1920x600 -> 960x300 x2): more of the world is visible, nothing is stretched.
    /// The world follows this window's zoom setting (auto or fixed).
    ///
    /// The game's UI camera is different: its orthographic size is fixed in the scene for 270 pixels of
    /// height (8.4375 = 270 / 32). Giving it the world's render height (e.g. 240 at x3) shrank the whole UI
    /// inside a smaller texture, which dropped rows of pixels from every glyph (unreadable text). So the UI
    /// always renders exactly 270 pixels tall (1:1 with its ortho size), only its width follows the window:
    /// same UI size and sharpness as the game in a normal window, whatever the world zoom.
    /// </summary>
    internal static class AspectFill
    {
        private static int _lastW, _lastH;

        public static bool Enabled => Settings.FillWindow && Duo.Active && WindowLayout.IsSplit(WindowLayout.Current);

        public static void Compute(out int w, out int h)
        {
            int sw = Screen.width, sh = Screen.height;
            int scale = Settings.PixelScaleFor(sh);
            h = sh / scale; w = sw / scale;
            if (h % 2 != 0) h--;
            if (w % 2 != 0) w--;
            if (w != _lastW || h != _lastH)
            {
                _lastW = w; _lastH = h;
                Debug.Log($"{CouchCoopMod.Tag} widened render: window {sw}x{sh} -> game {w}x{h} (x{scale})");
            }
        }

        public const int UiHeight = 270;
        private static int _lastUiW;

        /// <summary>UI render size: 270 pixels tall (the UI camera's fixed ortho size), width matching the window.</summary>
        public static void ComputeUi(out int w, out int h)
        {
            h = UiHeight;
            w = Mathf.CeilToInt(UiHeight * (float)Screen.width / Mathf.Max(1, Screen.height));
            if (w % 2 != 0) w++;
            if (w != _lastUiW)
            {
                _lastUiW = w;
                Debug.Log($"{CouchCoopMod.Tag} UI render: window {Screen.width}x{Screen.height} -> UI {w}x{h}");
            }
        }

        private static Camera _uiCam;

        /// <summary>The game's UI camera (menus, HUD, title screen), recognised by reference or by name.</summary>
        public static bool IsUiCamera(Camera src)
        {
            if (src == null) return false;
            if (ReferenceEquals(src, _uiCam)) return true;
            try
            {
                var cm = Manager.camera;
                if (cm != null && ReferenceEquals(cm.uiCamera, src)) { _uiCam = src; return true; }
            }
            catch { /* Manager not ready yet (loading screen) */ }
            if (src.name == "UI Camera") { _uiCam = src; return true; }
            return false;
        }

        /// <summary>
        /// The UI camera always uses the UI size. Outside the game (title screen, menus) the world camera
        /// uses it too, exactly like the unmodded game where both are 270 pixels tall: the title menu draws
        /// its selection bar with the world camera and its text with the UI camera, so different sizes put
        /// the bar on the wrong line. The zoom only applies in game.
        /// </summary>
        public static bool UseUiSize(Camera src)
        {
            if (IsUiCamera(src)) return true;
            try { return Manager.main == null || Manager.main.currentSceneHandler == null || !Manager.main.currentSceneHandler.isInGame; }
            catch { return true; }
        }

        public static bool AppliesTo(PugCamera cam, Camera src)
        {
            if (!Enabled || cam == null) return false;
            var mode = cam.GetOutputMode(src);
            return mode == OutputMode.MatchAspect || mode == OutputMode.Fixed;
        }
    }

    [HarmonyPatch(typeof(PugCamera), nameof(PugCamera.GetPixelWidth))]
    internal static class PixelWidthPatch
    {
        private static void Postfix(PugCamera __instance, Camera srcCamera, ref int __result)
        {
            if (!AspectFill.AppliesTo(__instance, srcCamera)) return;
            int w;
            if (AspectFill.UseUiSize(srcCamera ?? __instance.camera)) AspectFill.ComputeUi(out w, out _);
            else AspectFill.Compute(out w, out _);
            __result = w;
        }
    }

    [HarmonyPatch(typeof(PugCamera), nameof(PugCamera.GetPixelHeight))]
    internal static class PixelHeightPatch
    {
        private static void Postfix(PugCamera __instance, Camera srcCamera, ref int __result)
        {
            if (!AspectFill.AppliesTo(__instance, srcCamera)) return;
            int h;
            if (AspectFill.UseUiSize(srcCamera ?? __instance.camera)) AspectFill.ComputeUi(out _, out h);
            else AspectFill.Compute(out _, out h);
            __result = h;
        }
    }

    /// <summary>The final image is no longer letterboxed to 16:9: it fills the whole window.</summary>
    [HarmonyPatch(typeof(BilinearQuadAutoSizer), "LateUpdate")]
    internal static class BilinearQuadPatch
    {
        private static bool Prefix(BilinearQuadAutoSizer __instance)
        {
            if (!AspectFill.Enabled || __instance.bilinearQuadCamera == null || __instance.quad == null) return true;
            float h = __instance.bilinearQuadCamera.orthographicSize * 2f;
            __instance.quad.transform.localScale = new Vector3(h * __instance.bilinearQuadCamera.aspect, h, 1f);
            return false;
        }
    }
}
