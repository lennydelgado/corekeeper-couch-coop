using System;
using System.Collections.Generic;
using System.IO;
using Shared;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Controller -> player assignment, shared by the P1 and P2 windows:
    /// persistentDataPath/CouchCoop/controllers.json (outside the save folder, so common to both).
    /// Stored as "slot|name|key" strings: JsonUtility skips lists of objects from a runtime-loaded assembly.
    /// </summary>
    internal static class PadStore
    {
        [Serializable]
        private class FileData
        {
            public List<string> controllers = new List<string>();
        }

        private static List<string> _entries = new List<string>();
        private static string _lastRaw;

        private static string Dir => Path.Combine(Application.persistentDataPath, "CouchCoop");
        private static string FilePath => Path.Combine(Dir, "controllers.json");

        public static void Load()
        {
            _lastRaw = SafeFile.ReadAllTextOrNull(FilePath);
            if (_lastRaw != null)
            {
                _entries = Parse(_lastRaw);
                return;
            }
            MigrateFromMacPad();
        }

        /// <summary>Reloads the file if the other window changed it. True if the assignments changed.</summary>
        public static bool Reload()
        {
            string raw = SafeFile.ReadAllTextOrNull(FilePath);
            if (raw == null || raw == _lastRaw) return false;
            _lastRaw = raw;
            var parsed = Parse(raw);
            if (string.Join("\n", parsed) == string.Join("\n", _entries)) return false;
            _entries = parsed;
            return true;
        }

        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(new FileData { controllers = _entries }, true);
                SafeFile.WriteAllText(FilePath, json);
                _lastRaw = json;
            }
            catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} controllers not saved: {e.Message}"); }
        }

        public static int GetSlot(string key)
        {
            foreach (var e in _entries)
                if (Split(e, out int slot, out _, out string k) && k == key) return slot;
            return -1; // unknown
        }

        public static void SetSlot(string key, string name, int slot)
        {
            string entry = $"{slot}|{(name ?? "Controller").Replace("|", "/")}|{key}";
            for (int i = 0; i < _entries.Count; i++)
                if (Split(_entries[i], out _, out _, out string k) && k == key) { _entries[i] = entry; return; }
            _entries.Add(entry);
        }

        private static List<string> Parse(string raw)
        {
            try { return JsonUtility.FromJson<FileData>(raw)?.controllers ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        private static bool Split(string entry, out int slot, out string name, out string key)
        {
            slot = 0; name = null; key = null;
            var parts = entry?.Split(new[] { '|' }, 3);
            if (parts == null || parts.Length < 3 || !int.TryParse(parts[0], out slot)) return false;
            name = parts[1];
            key = parts[2];
            return true;
        }

        /// <summary>Imports the assignments of the "MacPad" development builds.</summary>
        [Serializable] private class LegacyMacPad { public List<string> padSlots = new List<string>(); }

        private static void MigrateFromMacPad()
        {
            string legacy = SafeFile.ReadAllTextOrNull(Path.Combine(Application.persistentDataPath, "MacPad.json"));
            if (legacy == null) return;
            try
            {
                var old = JsonUtility.FromJson<LegacyMacPad>(legacy);
                if (old?.padSlots == null || old.padSlots.Count == 0) return;
                _entries = old.padSlots;
                Save();
                Debug.Log($"{CouchCoopMod.Tag} imported {_entries.Count} controller(s) from MacPad.json");
            }
            catch { }
        }
    }
}
