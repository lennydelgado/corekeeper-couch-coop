using System.Collections.Generic;
using UnityEngine;

namespace CouchCoop.UI
{
    /// <summary>
    /// Draws the mod's UI with the game's own art: its bitmap fonts (PugFont) and its UI sprites
    /// (menu frame, item tooltip box, hotbar slot, check boxes). Everything is laid out in "game pixels"
    /// and scaled by an integer factor, exactly like the game's UI, so it stays crisp on a TV.
    /// Falls back to plain IMGUI if the assets are not loaded yet.
    /// </summary>
    internal static class GameUI
    {
        public enum Frame { Window, Box, Slot, CheckOff, CheckOn }
        public enum Font { Small, Medium, Large }

        public static readonly Color White = new Color(1f, 1f, 1f);
        public static readonly Color Dim = new Color(0.66f, 0.70f, 0.80f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.42f);
        public static readonly Color P1 = new Color(0.55f, 0.75f, 1f);
        public static readonly Color P2 = new Color(1f, 0.58f, 0.48f);
        public static readonly Color Ok = new Color(0.50f, 0.92f, 0.55f);
        public static readonly Color Warn = new Color(1f, 0.78f, 0.35f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.45f);

        private static readonly string[] SpriteNames = { "32x32_menu_border", "32x32_itemui_border", "hot_bar_border", "checkBox_0", "checkBox_1" };
        private static readonly Dictionary<Frame, Sprite> Sprites = new Dictionary<Frame, Sprite>();
        private static float _nextSearch;

        /// <summary>Integer UI scale: the "scale" setting, or auto (≈270 game pixels of height, like the game).</summary>
        public static int Px => Settings.PixelScaleFor(Screen.height);

        // ---------------------------------------------------------------- frames

        public static void Draw(Frame frame, Rect r, Color? tint = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            var s = GetSprite(frame);
            var old = GUI.color;
            GUI.color = tint ?? Color.white;
            if (s != null) DrawNineSlice(s, r, Px);
            else FallbackBox(r, frame);
            GUI.color = old;
        }

        private static Sprite GetSprite(Frame f)
        {
            if (Sprites.TryGetValue(f, out var s) && s != null) return s;
            if (Time.unscaledTime < _nextSearch) return null;
            _nextSearch = Time.unscaledTime + 2f; // sprites appear once the game's menus are loaded
            foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sp == null) continue;
                int i = System.Array.IndexOf(SpriteNames, sp.name);
                if (i >= 0) Sprites[(Frame)i] = sp;
            }
            Sprites.TryGetValue(f, out s);
            return s;
        }

        private static void DrawNineSlice(Sprite s, Rect r, int px)
        {
            var tex = s.texture;
            Rect tr = s.textureRect;
            Vector4 b = s.border; // L, B, R, T in sprite pixels
            if (b == Vector4.zero) { Blit(tex, tr, r); return; }

            float l = b.x * px, bo = b.y * px, ri = b.z * px, t = b.w * px;
            // Shrink borders when the target is smaller than the corners.
            float kx = Mathf.Min(1f, r.width / Mathf.Max(1f, l + ri)), ky = Mathf.Min(1f, r.height / Mathf.Max(1f, t + bo));
            l *= kx; ri *= kx; t *= ky; bo *= ky;

            float[] xs = { r.x, r.x + l, r.xMax - ri, r.xMax };
            float[] ys = { r.y, r.y + t, r.yMax - bo, r.yMax }; // screen space, top → bottom
            float[] us = { tr.x, tr.x + b.x, tr.xMax - b.z, tr.xMax };
            float[] vs = { tr.yMax, tr.yMax - b.w, tr.y + b.y, tr.y }; // texture space, top → bottom
            for (int iy = 0; iy < 3; iy++)
            for (int ix = 0; ix < 3; ix++)
            {
                var dst = Rect.MinMaxRect(xs[ix], ys[iy], xs[ix + 1], ys[iy + 1]);
                var src = Rect.MinMaxRect(us[ix], vs[iy + 1], us[ix + 1], vs[iy]);
                if (dst.width > 0 && dst.height > 0 && src.width > 0 && src.height > 0) Blit(tex, src, dst);
            }
        }

        private static void Blit(Texture2D tex, Rect srcPixels, Rect dst)
        {
            var uv = new Rect(srcPixels.x / tex.width, srcPixels.y / tex.height, srcPixels.width / tex.width, srcPixels.height / tex.height);
            GUI.DrawTextureWithTexCoords(dst, tex, uv, true);
        }

        private static void FallbackBox(Rect r, Frame f)
        {
            var c = f == Frame.Window ? new Color(0.13f, 0.14f, 0.16f, 0.96f) : f == Frame.Slot ? new Color(0.55f, 0.36f, 0.16f, 0.95f) : new Color(0.26f, 0.33f, 0.47f, 0.95f);
            var old = GUI.color;
            GUI.color = old * c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ---------------------------------------------------------------- text

        private static PugFont GetFont(Font f)
        {
            try
            {
                var tm = Manager.text;
                if (tm == null) return null;
                var font = f == Font.Small ? tm.thinSmall : f == Font.Large ? tm.boldLarge : tm.boldMedium;
                return font != null && font.texture != null && font.codePoints != null && font.codePoints.Count > 0 ? font : null;
            }
            catch { return null; }
        }

        /// <summary>Height of a text line in screen pixels.</summary>
        public static float LineHeight(Font f)
        {
            var font = GetFont(f);
            int h = font != null ? font.charDims.y : (f == Font.Small ? 8 : f == Font.Large ? 14 : 10);
            return h * Px;
        }

        public static void Text(Rect r, string text, Font f, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool shadow = true)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            var font = GetFont(f);
            if (font == null) { FallbackText(r, text, f, color, anchor); return; }

            int px = Px;
            // Step the scale down (never below 1) if the line does not fit.
            while (px > 1 && Measure(font, text) * px > r.width) px--;

            string[] lines = text.Split('\n');
            float lineH = (font.charDims.y + font.lineSpacing) * px;
            float blockH = lines.Length * lineH - font.lineSpacing * px;
            float y = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.UpperCenter || anchor == TextAnchor.UpperRight ? r.y
                : anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight ? r.yMax - blockH
                : r.y + (r.height - blockH) / 2f;

            var old = GUI.color;
            foreach (var line in lines)
            {
                float w = Measure(font, line) * px;
                float x = anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.UpperCenter || anchor == TextAnchor.LowerCenter ? r.x + (r.width - w) / 2f
                    : anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight ? r.xMax - w : r.x;
                x = Mathf.Round(x); float yy = Mathf.Round(y);
                if (shadow) { GUI.color = new Color(0f, 0f, 0f, 0.75f * color.a); DrawLine(font, line, x + px, yy + px, px); }
                GUI.color = color;
                DrawLine(font, line, x, yy, px);
                y += lineH;
            }
            GUI.color = old;
        }

        private static string Prepare(PugFont font, string s) => font.allCaps ? s.ToUpperInvariant() : s;

        private static bool TryGlyph(PugFont font, char c, out PugFont.GlyphData g)
        {
            g = null;
            if (font.codePoints.TryGetValue(c, out int i) || font.codePoints.TryGetValue(char.ToUpperInvariant(c), out i)
                || font.codePoints.TryGetValue(char.ToLowerInvariant(c), out i))
            {
                g = font.glyphData[i];
                return g != null && g.rect.width > 0;
            }
            return false;
        }

        private static float Measure(PugFont font, string s)
        {
            float w = 0;
            foreach (char c in Prepare(font, s))
            {
                if (c == ' ') { w += font.spaceWidth + font.charSpacing; continue; }
                if (TryGlyph(font, c, out var g)) w += g.rect.width + font.charSpacing;
            }
            return Mathf.Max(0, w - font.charSpacing);
        }

        private static void DrawLine(PugFont font, string s, float x, float y, int px)
        {
            var tex = font.texture;
            int cellH = font.charDims.y;
            foreach (char c in Prepare(font, s))
            {
                if (c == ' ') { x += (font.spaceWidth + font.charSpacing) * px; continue; }
                if (!TryGlyph(font, c, out var g)) continue;
                var rc = g.rect;
                var dst = new Rect(x, y + (cellH - rc.height) * px, rc.width * px, rc.height * px);
                Blit(tex, new Rect(rc.x, rc.y, rc.width, rc.height), dst);
                x += (rc.width + font.charSpacing) * px;
            }
        }

        private static GUIStyle _fallbackStyle;

        private static void FallbackText(Rect r, string text, Font f, Color color, TextAnchor anchor)
        {
            if (_fallbackStyle == null) _fallbackStyle = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip };
            _fallbackStyle.fontSize = Mathf.RoundToInt(LineHeight(f) * 0.9f);
            _fallbackStyle.alignment = anchor;
            _fallbackStyle.normal.textColor = color;
            GUI.Label(r, text, _fallbackStyle);
        }
    }
}
