using System;
using PugMod;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Settings through the official PugMod.API.Config (one small JSON per setting in
    /// .../Core Keeper/Steam/&lt;id&gt;/mods/CouchCoop/). Also editable in-game from the hub's Options tab.
    /// If the API fails (e.g. NotProton without NotProtonFixes) the defaults are kept in memory.
    /// </summary>
    internal static class Settings
    {
        private sealed class Entry<T>
        {
            private readonly IConfigEntry<T> _entry;
            private T _value;

            public Entry(string section, string key, string description, T def)
            {
                _value = def;
                try
                {
                    _entry = API.Config?.Register(CouchCoopMod.Name, section, description, key, def);
                    if (_entry != null) _value = _entry.Value;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{CouchCoopMod.Tag} setting {key} unreadable ({e.Message}), using default");
                }
            }

            public T Value
            {
                get => _value;
                set
                {
                    _value = value;
                    try { if (_entry != null) _entry.Value = value; }
                    catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} setting not saved: {e.Message}"); }
                }
            }
        }

        public const int MaxScale = 4;

        private static Entry<string> _layout, _password;
        private static Entry<bool> _fill, _muteMusic, _autoFillJoin, _autoOpen;
        private static Entry<int> _sourceWorld, _scale;

        public static void Load()
        {
            _layout = new Entry<string>("Duo", "layout", "Window layout: stacked (P2 top, P1 bottom) | side (P1 left, P2 right)", "stacked");
            _fill = new Entry<bool>("Duo", "fillWindow", "Widen the render to fill each half of the screen (no black bars)", true);
            _muteMusic = new Entry<bool>("Duo", "muteJ2Music", "Mute the music of the P2 window (sound effects stay on)", true);
            _autoFillJoin = new Entry<bool>("Duo", "autoFillJoin", "Pre-fill Join With IP with the local server address", true);
            _sourceWorld = new Entry<int>("Duo", "sourceWorld", "World (0-29) copied into the local server the first time split screen starts", 0);
            _password = new Entry<string>("Duo", "serverPassword", "Local server password (generated automatically)", "");
            _scale = new Entry<int>("Display", "scale", "Pixel scale in split screen and for the mod's menus: 0 = auto, 1-4 = fixed (bigger = larger UI, closer view)", 0);
            _autoOpen = new Entry<bool>("Controllers", "autoOpenSelector", "Open the controller picker when a new controller is plugged in", true);

            if (string.IsNullOrEmpty(_password.Value))
                _password.Value = Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        public static bool SideBySide { get => _layout?.Value == "side"; set => _layout.Value = value ? "side" : "stacked"; }
        public static bool FillWindow { get => _fill?.Value ?? true; set => _fill.Value = value; }
        public static bool MuteJ2Music { get => _muteMusic?.Value ?? true; set => _muteMusic.Value = value; }
        public static bool AutoFillJoin { get => _autoFillJoin?.Value ?? true; set => _autoFillJoin.Value = value; }
        public static bool AutoOpenSelector { get => _autoOpen?.Value ?? true; set => _autoOpen.Value = value; }
        public static int SourceWorld { get => Mathf.Clamp(_sourceWorld?.Value ?? 0, 0, 29); set => _sourceWorld.Value = Mathf.Clamp(value, 0, 29); }
        /// <summary>0 = auto, 1..MaxScale = fixed integer pixel scale.</summary>
        public static int Scale { get => Mathf.Clamp(_scale?.Value ?? 0, 0, MaxScale); set => _scale.Value = Mathf.Clamp(value, 0, MaxScale); }
        public static string ServerPassword => _password?.Value ?? "";

        /// <summary>Integer pixel scale for a window of the given height (auto ≈ 270 game pixels tall, never below 135).</summary>
        public static int PixelScaleFor(int screenHeight)
        {
            int max = Mathf.Max(1, screenHeight / 135);
            int s = Scale > 0 ? Scale : Mathf.RoundToInt(screenHeight / 270f);
            return Mathf.Clamp(s, 1, max);
        }
    }
}
