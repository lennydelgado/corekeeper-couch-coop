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
    /// Worlds of the local server (indexes 0-29 in its data path) and their link with P1's solo worlds:
    ///  - several worlds can live on the server; <see cref="Settings.ServerWorld"/> is the one split screen loads;
    ///  - "new world": the server generates it on the next start, with its name and mode (-worldname,
    ///    -worldmode, random seed). For full generation settings, create the world in the game, then add it;
    ///  - "add from solo": a solo world is copied into a new server index (nothing on the server is replaced);
    ///  - "bring back": the selected server world replaces the solo world it is linked to (same guid, or the
    ///    slot it was brought back to before), or goes into the first free solo slot. The replaced solo world
    ///    is backed up first (<see cref="WorldBackup"/>);
    ///  - "rename": changes the name in the server worldinfo. It is the name players see when they join, and the
    ///    solo world gets it when the world is brought back.
    /// Copies only run while no server is running, so the files are final and nothing writes them meanwhile.
    ///
    /// The dedicated server never writes a worldinfo (SaveManager.WriteWorldInfo only logs an error there), so
    /// the mod keeps the server worldinfos itself: copied from the solo world, or created for a new world.
    /// </summary>
    internal static class ServerWorld
    {
        public const int MaxSlots = 30;
        public const int MaxNameLength = 24;

        /// <summary>Name of a world the server generated without a worldinfo (first start without a solo world).</summary>
        public const string GeneratedName = "Couch Co-op";

        [Serializable]
        private class InfoHeader
        {
            public string name = "";
            public string guid = "";
            public int mode;
        }

        public struct Slot
        {
            public int Index;
            public string Name, Guid;
            public bool Generated; // worldinfo only: the server creates the world on its next start
        }

        private static StandaloneFilesystem MainFs =>
            AccessTools.Field(typeof(Manager), "_filesystemInterface")?.GetValue(null) as StandaloneFilesystem;

        // ---------------- paths ----------------

        public static string SoloWorld(int i) => MainFs?.Rel2Abs($"worlds/{i}.world.gzip");
        public static string SoloInfo(int i) => MainFs?.Rel2Abs($"worldinfos/{i}.worldinfo");
        private static string SoloGen(int i) => MainFs?.Rel2Abs($"worldgenparams/{i}.json");

        public static string WorldFile(int i) => Path.Combine(Duo.ServerDataPath, "worlds", $"{i}.world.gzip");
        public static string InfoFile(int i) => Path.Combine(Duo.ServerDataPath, "worldinfos", $"{i}.worldinfo");
        private static string GenFile(int i) => Path.Combine(Duo.ServerDataPath, "worldgenparams", $"{i}.json");
        /// <summary>Solo slot this server world was last brought back to (the server world has no real guid in its worldinfo).</summary>
        private static string LinkFile(int i) => Path.Combine(Duo.ServerDataPath, "worldinfos", $"{i}.couchcoop-solo.txt");

        public static int Current => Settings.ServerWorld;
        public static string CurrentWorldFile => WorldFile(Current);
        public static string CurrentInfoFile => InfoFile(Current);

        // ---------------- reading ----------------

        private static bool ReadHeader(string infoPath, out InfoHeader h)
        {
            h = null;
            try
            {
                string raw = infoPath != null ? SafeFile.ReadAllTextOrNull(infoPath) : null;
                if (string.IsNullOrEmpty(raw)) return false;
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

        /// <summary>Server world i: a world file, or a worldinfo waiting for the server to generate it.</summary>
        public static bool TryGetServer(int i, out Slot slot)
        {
            slot = default;
            bool world = SafeFile.Exists(WorldFile(i));
            bool info = ReadHeader(InfoFile(i), out var h);
            if (!world && !info) return false;
            slot = new Slot
            {
                Index = i,
                Name = info && !string.IsNullOrEmpty(h.name) ? h.name : GeneratedName,
                Guid = info ? h.guid : "",
                Generated = !world,
            };
            return true;
        }

        public static string CurrentName => TryGetServer(Current, out var s) ? s.Name : GeneratedName;

        /// <summary>World mode the server should generate a new world with (-worldmode), or null.</summary>
        public static string PendingMode(int i)
        {
            if (SafeFile.Exists(WorldFile(i)) || !ReadHeader(InfoFile(i), out var h)) return null;
            return ((WorldMode)h.mode).ToString();
        }

        /// <summary>Next existing solo (or server) world after (dir = 1) or before (dir = -1) slot i, or i if there is none.</summary>
        public static int NextSolo(int i, int dir) => Next(i, dir, j => TryGetSolo(j, out _));
        public static int NextServer(int i, int dir) => Next(i, dir, j => TryGetServer(j, out _));

        private static int Next(int i, int dir, Func<int, bool> exists)
        {
            for (int k = 1; k <= MaxSlots; k++)
            {
                int j = ((i + dir * k) % MaxSlots + MaxSlots) % MaxSlots;
                if (exists(j)) return j;
            }
            return i;
        }

        private static int FirstFreeServer()
        {
            for (int i = 0; i < MaxSlots; i++)
                if (!SafeFile.Exists(WorldFile(i)) && !SafeFile.Exists(InfoFile(i))) return i;
            return -1;
        }

        /// <summary>Solo slot the current server world goes back to: its linked one, else the first free one (-1 if full).</summary>
        public static int BringBackTarget(out bool sameWorld)
        {
            sameWorld = false;
            ReadHeader(CurrentInfoFile, out var server);
            if (int.TryParse(SafeFile.ReadAllTextOrNull(LinkFile(Current))?.Trim(), out int linked) && TryGetSolo(linked, out _))
            {
                sameWorld = true;
                return linked;
            }
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

        // ---------------- actions (each returns the message to show) ----------------

        /// <summary>Picks the server world split screen loads next time.</summary>
        public static string Select(int dir)
        {
            if (!NoServerRunning) return L10n.T("world.stopFirst");
            Settings.ServerWorld = NextServer(Current, dir);
            return null;
        }

        /// <summary>Creates a new server world (worldinfo only); the server generates it on the next start.</summary>
        public static string CreateNew(WorldMode mode)
        {
            if (!NoServerRunning) return L10n.T("world.stopFirst");
            int i = FirstFreeServer();
            if (i < 0) return L10n.T("world.serverFull");
            string name = UniqueName(L10n.T("world.newName"));
            if (!WriteInfo(InfoFile(i), new WorldInfo { version = WorldInfo.CURRENT_VERSION, name = name, mode = mode, creationDate = new CreationDate(DateTime.Now) }))
                return L10n.T("world.failed");
            Settings.ServerWorld = i;
            Debug.Log($"{CouchCoopMod.Tag} new server world {i}: {name} ({mode}), generated on the next start");
            return string.Format(L10n.T("world.created"), name);
        }

        /// <summary>Copies solo world i into a new server index and selects it.</summary>
        public static string AddFromSolo(int solo)
        {
            if (!NoServerRunning) return L10n.T("world.stopFirst");
            if (!TryGetSolo(solo, out var slot)) return L10n.T("world.noSolo");
            int i = FirstFreeServer();
            if (i < 0) return L10n.T("world.serverFull");
            try
            {
                SafeFile.WriteAllBytes(WorldFile(i), File.ReadAllBytes(SoloWorld(solo)));
                SafeFile.WriteAllBytes(InfoFile(i), File.ReadAllBytes(SoloInfo(solo)));
                if (SoloGen(solo) != null && SafeFile.Exists(SoloGen(solo))) SafeFile.WriteAllBytes(GenFile(i), File.ReadAllBytes(SoloGen(solo)));
                SafeFile.WriteAllText(LinkFile(i), solo.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} world copy failed: {e.Message}");
                return L10n.T("world.failed");
            }
            Settings.ServerWorld = i;
            Debug.Log($"{CouchCoopMod.Tag} solo world {solo} ({slot.Name}) added to the local server as world {i}");
            return string.Format(L10n.T("world.added"), slot.Name);
        }

        /// <summary>Copies the current server world into P1's solo worlds.</summary>
        public static string BringBack()
        {
            if (!NoServerRunning) return L10n.T("world.stopFirst");
            if (!SafeFile.Exists(CurrentWorldFile)) return L10n.T("world.noServerWorld");
            int target = BringBackTarget(out bool same);
            if (target < 0) return L10n.T("world.noFreeSlot");
            string name = CurrentName;

            if (same) WorldBackup.Save(WorldBackup.BeforeReplace, $"solo world {target} before bringing server world {Current} back", SoloWorld(target), SoloInfo(target));
            try
            {
                SafeFile.WriteAllBytes(SoloWorld(target), File.ReadAllBytes(CurrentWorldFile));
                if (same)
                {
                    // Keep the solo worldinfo (crystals, icon...), only take the server world's name.
                    var info = ReadInfo(SoloInfo(target));
                    if (info != null && info.name != name) { info.name = name; WriteInfo(SoloInfo(target), info); }
                }
                else
                {
                    var info = ReadInfo(CurrentInfoFile) ?? new WorldInfo { version = WorldInfo.CURRENT_VERSION, creationDate = new CreationDate(DateTime.Now) };
                    info.name = name;
                    WriteInfo(SoloInfo(target), info);
                    if (SafeFile.Exists(GenFile(Current)) && SoloGen(target) != null) SafeFile.WriteAllBytes(SoloGen(target), File.ReadAllBytes(GenFile(Current)));
                }
                SafeFile.WriteAllText(LinkFile(Current), target.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} world copy failed: {e.Message}");
                return L10n.T("world.failed");
            }
            // The game read every worldinfo at startup: refresh this slot so the world list shows it now.
            try { Manager.saves.ReloadWorldInfo(target); } catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} world list refresh: {e.Message}"); }
            Debug.Log($"{CouchCoopMod.Tag} server world {Current} ({name}) copied into solo slot {target} (same world: {same})");
            return string.Format(L10n.T("world.broughtBack"), name, target);
        }

        /// <summary>Renames the current server world (applies when the server starts, and when it is brought back).</summary>
        public static string Rename(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return null;
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
            var info = ReadInfo(CurrentInfoFile) ?? new WorldInfo { version = WorldInfo.CURRENT_VERSION, creationDate = new CreationDate(DateTime.Now) };
            info.name = name;
            if (!WriteInfo(CurrentInfoFile, info)) return L10n.T("world.failed");
            Debug.Log($"{CouchCoopMod.Tag} server world {Current} renamed to {name}");
            return string.Format(L10n.T("world.renamed"), name);
        }

        // ---------------- helpers ----------------

        private static WorldInfo ReadInfo(string path)
        {
            try
            {
                string raw = SafeFile.ReadAllTextOrNull(path);
                return string.IsNullOrEmpty(raw) ? null : JsonUtility.FromJson<WorldInfo>(raw);
            }
            catch { return null; }
        }

        private static bool WriteInfo(string path, WorldInfo info)
        {
            try
            {
                SafeFile.WriteAllText(path, JsonUtility.ToJson(info));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} worldinfo not written ({path}): {e.Message}");
                return false;
            }
        }

        private static string UniqueName(string baseName)
        {
            for (int n = 1; n < 100; n++)
            {
                string candidate = n == 1 ? baseName : $"{baseName} {n}";
                bool taken = false;
                for (int i = 0; i < MaxSlots && !taken; i++)
                    if (TryGetServer(i, out var s) && s.Name == candidate) taken = true;
                if (!taken) return candidate;
            }
            return baseName;
        }
    }
}
