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
    /// The scale follows the "scale" setting (auto or fixed), which also sizes the game's UI.
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
            AspectFill.Compute(out int w, out _);
            __result = w;
        }
    }

    [HarmonyPatch(typeof(PugCamera), nameof(PugCamera.GetPixelHeight))]
    internal static class PixelHeightPatch
    {
        private static void Postfix(PugCamera __instance, Camera srcCamera, ref int __result)
        {
            if (!AspectFill.AppliesTo(__instance, srcCamera)) return;
            AspectFill.Compute(out _, out int h);
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
