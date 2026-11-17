using System;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using Pug.Platform;
using Shared;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CouchCoop
{
    /// <summary>
    /// Moves worlds between P1's solo worlds and the local server, always with a backup of the world that gets
    /// replaced (see <see cref="WorldBackup"/>):
    ///  - "bring back": the server world replaces the solo world it came from (same guid), or goes into the first
    ///    free solo slot;
    ///  - "load": a solo world replaces the server world.
    /// Both only run while no server is running, so the files are final and nothing writes them meanwhile.
    ///
    /// The dedicated server never writes a worldinfo (SaveManager.WriteWorldInfo only logs an error there):
    ///  - a world it generated itself (new player without a solo world) has none: one is created when it is
    ///    brought back, named like the server world;
    ///  - a copied world keeps the worldinfo it was copied with, so bringing it back over the same solo world
    ///    keeps the solo worldinfo. The game refreshes guid, seed and bosses from the world when it loads it.
    /// </summary>
    internal static class ServerWorld
    {
        public const int MaxSlots = 30;

        [Serializable]
        private class InfoHeader
        {
            public string name = "";
            public string guid = "";
        }

        public struct Slot
        {
            public int Index;
            public string Name, Guid;
        }

        private static StandaloneFilesystem MainFs =>
            AccessTools.Field(typeof(Manager), "_filesystemInterface")?.GetValue(null) as StandaloneFilesystem;

        public static string SoloWorld(int i) => MainFs?.Rel2Abs($"worlds/{i}.world.gzip");
        public static string SoloInfo(int i) => MainFs?.Rel2Abs($"worldinfos/{i}.worldinfo");
        public static string ServerWorldFile => Path.Combine(Duo.ServerDataPath, "worlds", "0.world.gzip");
        public static string ServerInfoFile => Path.Combine(Duo.ServerDataPath, "worldinfos", "0.worldinfo");

        private static bool ReadHeader(string infoPath, out InfoHeader h)
        {
            h = null;
            try
            {
                string raw = infoPath != null ? SafeFile.ReadAllTextOrNull(infoPath) : null;
                if (raw == null) return false;
                h = JsonUtility.FromJson<InfoHeader>(raw);
                return h != null;
            }
            catch { return false; }
        }

        /// <summary>Solo world in slot i (name from its worldinfo), if there is one.</summary>
        public static bool TryGetSolo(int i, out Slot slot)
        {
            slot = default;
            if (MainFs == null || !SafeFile.Exists(SoloWorld(i)) || !ReadHeader(SoloInfo(i), out var h)) return false;
            slot = new Slot { Index = i, Name = string.IsNullOrEmpty(h.name) ? "?" : h.name, Guid = h.guid };
            return true;
        }

        /// <summary>Next existing solo world after (dir = 1) or before (dir = -1) slot i, or i if there is none.</summary>
        public static int NextSolo(int i, int dir)
        {
            for (int k = 1; k <= MaxSlots; k++)
            {
                int j = ((i + dir * k) % MaxSlots + MaxSlots) % MaxSlots;
                if (TryGetSolo(j, out _)) return j;
            }
            return i;
        }

        /// <summary>Name the server gives a world it generates (-worldname in Duo.StartServer).</summary>
        public const string GeneratedName = "Couch Co-op";

        public static string ServerWorldName => ReadHeader(ServerInfoFile, out var h) && !string.IsNullOrEmpty(h.name) ? h.name : GeneratedName;

        /// <summary>Solo slot the server world goes back to: the one with the same guid, else the first free one (-1 if full).</summary>
        public static int BringBackTarget(out bool sameWorld)
        {
            sameWorld = false;
            ReadHeader(ServerInfoFile, out var server); // none for a world the server generated itself
            int free = -1;
            for (int i = 0; i < MaxSlots; i++)
            {
                if (TryGetSolo(i, out var s))
                {
                    if (server != null && !string.IsNullOrEmpty(server.guid) && s.Guid == server.guid) { sameWorld = true; return i; }
                }
                else if (free < 0 && !SafeFile.Exists(SoloWorld(i)) && !SafeFile.Exists(SoloInfo(i))) free = i;
            }
            return free;
        }

        private static float _checkedAt = -10f;
        private static bool _serverProcessRunning;

        /// <summary>True when no CoreKeeperServer process is running (checked at most once per second).</summary>
        public static bool NoServerRunning
        {
            get
            {
                if (Duo.ServerRunning) return false;
                if (Time.unscaledTime - _checkedAt > 1f)
                {
                    _checkedAt = Time.unscaledTime;
                    try { _serverProcessRunning = Process.GetProcessesByName("CoreKeeperServer").Length > 0; }
                    catch { _serverProcessRunning = false; }
                }
                return !_serverProcessRunning;
            }
        }

        /// <summary>Copies the server world into P1's solo worlds. Returns the message to show.</summary>
        public static string BringBack()
        {
            if (!NoServerRunning) return L10n.T("world.stopFirst");
            if (!SafeFile.Exists(ServerWorldFile)) return L10n.T("world.noServerWorld");
            int target = BringBackTarget(out bool same);
            if (target < 0) return L10n.T("world.noFreeSlot");

            if (same) WorldBackup.Save(WorldBackup.BeforeReplace, $"solo world {target} before bringing the server world back", SoloWorld(target), SoloInfo(target));
            try
            {
                SafeFile.WriteAllBytes(SoloWorld(target), File.ReadAllBytes(ServerWorldFile));
                if (!same)
                {
                    // New solo slot: it needs a worldinfo (name, date). The server's, or a new one.
                    if (SafeFile.Exists(ServerInfoFile)) SafeFile.WriteAllBytes(SoloInfo(target), File.ReadAllBytes(ServerInfoFile));
                    else SafeFile.WriteAllText(SoloInfo(target), JsonUtility.ToJson(new WorldInfo
                    {
                        version = WorldInfo.CURRENT_VERSION,
                        name = GeneratedName,
                        creationDate = new CreationDate(DateTime.Now),
                    }));
                    string gen = Path.Combine(Duo.ServerDataPath, "worldgenparams", "0.json");
                    string genTo = MainFs?.Rel2Abs($"worldgenparams/{target}.json");
                    if (genTo != null && SafeFile.Exists(gen)) SafeFile.WriteAllBytes(genTo, File.ReadAllBytes(gen));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} world copy failed: {e.Message}");
                return L10n.T("world.failed");
            }
            // The game read every worldinfo at startup: refresh this slot so the world list shows it now.
            try { Manager.saves.ReloadWorldInfo(target); } catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} world list refresh: {e.Message}"); }
            Debug.Log($"{CouchCoopMod.Tag} server world copied into solo slot {target} (same world: {same})");
            return string.Format(L10n.T("world.broughtBack"), target);
        }

        /// <summary>Replaces the server world with solo world i. Returns the message to show.</summary>
        public static string LoadIntoServer(int i)
        {
            if (!NoServerRunning) return L10n.T("world.stopFirst");
            if (!TryGetSolo(i, out var slot)) return L10n.T("world.noSolo");

            WorldBackup.Save(WorldBackup.BeforeReplace, $"server world before loading solo world {i} ({slot.Name})", ServerWorldFile, ServerInfoFile);
            if (!Copy(SoloWorld(i), SoloInfo(i), ServerWorldFile, ServerInfoFile)) return L10n.T("world.failed");
            try
            {
                string genFrom = MainFs?.Rel2Abs($"worldgenparams/{i}.json");
                if (genFrom != null && SafeFile.Exists(genFrom))
                    SafeFile.WriteAllBytes(Path.Combine(Duo.ServerDataPath, "worldgenparams", "0.json"), File.ReadAllBytes(genFrom));
            }
            catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} worldgenparams not copied: {e.Message}"); }
            Debug.Log($"{CouchCoopMod.Tag} solo world {i} ({slot.Name}) loaded into the local server");
            return string.Format(L10n.T("world.loaded"), slot.Name);
        }

        private static bool Copy(string world, string info, string toWorld, string toInfo)
        {
            try
            {
                SafeFile.WriteAllBytes(toWorld, File.ReadAllBytes(world));
                if (SafeFile.Exists(info)) SafeFile.WriteAllBytes(toInfo, File.ReadAllBytes(info));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} world copy failed: {e.Message}");
                return false;
            }
        }
    }
}
