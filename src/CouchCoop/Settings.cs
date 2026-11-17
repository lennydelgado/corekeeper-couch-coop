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
        private static Entry<int> _sourceWorld, _scale, _scaleP2, _fpsLimit;

        public static void Load()
        {
            _layout = new Entry<string>("Duo", "layout", "Window layout: stacked (P2 top, P1 bottom) | side (P1 left, P2 right)", "stacked");
            _fill = new Entry<bool>("Duo", "fillWindow", "Widen the render to fill each half of the screen (no black bars)", true);
            _muteMusic = new Entry<bool>("Duo", "muteJ2Music", "Mute the music of the P2 window (sound effects stay on)", true);
            _autoFillJoin = new Entry<bool>("Duo", "autoFillJoin", "Pre-fill Join With IP with the local server address", true);
            _sourceWorld = new Entry<int>("Duo", "sourceWorld", "World (0-29) copied into the local server the first time split screen starts", 0);
            _password = new Entry<string>("Duo", "serverPassword", "Local server password (generated automatically)", "");
            _scale = new Entry<int>("Display", "scale", "P1 world zoom in split screen: 0 = auto, 1-4 = fixed (bigger = closer view). The game's UI keeps its normal size.", 0);
            _scaleP2 = new Entry<int>("Display", "scaleP2", "P2 world zoom in split screen: 0 = auto, 1-4 = fixed (set from the P2 window)", 0);
            _fpsLimit = new Entry<int>("Performance", "fpsLimit", "Frame rate cap of each window in split screen (0 = game setting). 60 keeps two windows + the server smooth.", 60);
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
        public static int ScaleP2 { get => Mathf.Clamp(_scaleP2?.Value ?? 0, 0, MaxScale); set => _scaleP2.Value = Mathf.Clamp(value, 0, MaxScale); }
        /// <summary>World zoom of THIS window (P1 and P2 each have their own).</summary>
        public static int MyScale { get => Duo.IsJ2 ? ScaleP2 : Scale; set { if (Duo.IsJ2) ScaleP2 = value; else Scale = value; } }
        public static int FpsLimit { get => Mathf.Clamp(_fpsLimit?.Value ?? 60, 0, 240); set => _fpsLimit.Value = Mathf.Clamp(value, 0, 240); }
        public static string ServerPassword => _password?.Value ?? "";

        /// <summary>Integer world pixel scale of this window (auto ≈ 270 game pixels tall, never below 135).</summary>
        public static int PixelScaleFor(int screenHeight)
        {
            int max = Mathf.Max(1, screenHeight / 135);
            int s = MyScale > 0 ? MyScale : AutoScaleFor(screenHeight);
            return Mathf.Clamp(s, 1, max);
        }

        /// <summary>Scale the game itself would pick (≈270 game pixels tall). Used for the mod's own menus.</summary>
        public static int AutoScaleFor(int screenHeight) => Mathf.Max(1, Mathf.RoundToInt(screenHeight / 270f));
    }
}
