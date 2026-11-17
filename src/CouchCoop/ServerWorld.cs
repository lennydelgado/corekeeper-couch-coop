using System;
using System.IO;
using Pug.Platform;
using Shared;
using UnityEngine;

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

        /// <summary>
        /// Prefix of a world brought back to the solo worlds, so a Couch Co-op world is easy to spot in the solo
        /// list. Server names never carry it: it is added on bring back and removed on add from solo.
        /// </summary>
        public const string SoloPrefix = "Coop: ";

        public static string WithPrefix(string name) =>
            name.StartsWith(SoloPrefix, StringComparison.Ordinal) ? name : SoloPrefix + name;

        public static string WithoutPrefix(string name) =>
            name != null && name.StartsWith(SoloPrefix, StringComparison.Ordinal) ? name.Substring(SoloPrefix.Length) : name;

        /// <summary>Name of a world the server generated without a worldinfo (first start without a solo world).</summary>
        public const string GeneratedName = "Couch Co-op";

        [Serializable]
        private class InfoHeader
        {
            public string name = "";
            public string guid = "";
            public int mode = 0; // filled by JsonUtility
        }

        public struct Slot
        {
            public int Index;
            public string Name, Guid;
            public bool Generated; // worldinfo only: the server creates the world on its next start
        }

        private static StandaloneFilesystem MainFs => Duo.MainFilesystem;

        // ---------------- paths ----------------

        public static string SoloWorld(int i) => MainFs?.Rel2Abs($"worlds/{i}.world.gzip");
        public static string SoloInfo(int i) => MainFs?.Rel2Abs($"worldinfos/{i}.worldinfo");
        private static string SoloGen(int i) => MainFs?.Rel2Abs($"worldgenparams/{i}.json");

        public static string WorldFile(int i) => Path.Combine(Duo.ServerDataPath, "worlds", $"{i}.world.gzip");
        public static string InfoFile(int i) => Path.Combine(Duo.ServerDataPath, "worldinfos", $"{i}.worldinfo");
        private static string GenFile(int i) => Path.Combine(Duo.ServerDataPath, "worldgenparams", $"{i}.json");
        /// <summary>
        /// Solo world this server world is linked to (added from it, or brought back to it): "slot", "guid", "name"
        /// on three lines. A server world it generated has no real guid in its worldinfo, so the link is how bring
        /// back finds the same solo world again. The guid (or, before the game first loads the solo world and
        /// gives it one, the name) must still match: a different world created in that slot since is never replaced.
        /// </summary>
        private static string LinkFile(int i) => Path.Combine(Duo.ServerDataPath, "worldinfos", $"{i}.couchcoop-solo.txt");

        private static void WriteLink(int server, int solo)
        {
            ReadHeader(SoloInfo(solo), out var h);
            SafeFile.WriteAllText(LinkFile(server), $"{solo}\n{h?.guid ?? ""}\n{h?.name ?? ""}");
        }

        private static bool TryReadLink(int server, out int solo)
        {
            solo = -1;
            string[] lines = (SafeFile.ReadAllTextOrNull(LinkFile(server)) ?? "").Split('\n');
            if (!int.TryParse(lines[0].Trim(), out int slot) || !TryGetSolo(slot, out var s)) return false;
            string guid = lines.Length > 1 ? lines[1].Trim() : "", name = lines.Length > 2 ? lines[2].Trim() : "";
            bool same = guid.Length > 0 ? s.Guid == guid : name.Length > 0 && s.Name == name;
            if (same) solo = slot;
            return same;
        }

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
            if (TryReadLink(Current, out int linked))
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
        private static bool _leftoverRunning;

        /// <summary>
        /// True when the local server is not running: neither the one of this session nor one left by a crashed
        /// session (checked at most once per second). A dedicated server the player runs themself does not count.
        /// </summary>
        public static bool NoServerRunning
        {
            get
            {
                if (Duo.ServerRunning) return false;
                if (Time.unscaledTime - _checkedAt > 1f)
                {
                    _checkedAt = Time.unscaledTime;
                    _leftoverRunning = Duo.LeftoverServerRunning();
                }
                return !_leftoverRunning;
            }
        }

        // ---------------- cached view for the hub ----------------

        /// <summary>What the Worlds tab shows. Reading it scans up to 60 files, so the hub reads it from <see cref="View"/>.</summary>
        public sealed class Snapshot
        {
            public Slot Server;
            public bool ServerExists, WorldGenerated;
            public int BackTarget = -1;
            public bool BackSame;
            public string BackName;
            public bool HasSolo;
            public Slot Solo;
        }

        private static Snapshot _view;
        private static float _viewAt = -10f;

        /// <summary>Worlds tab data, refreshed at most once per second, and right after any change made here.</summary>
        public static Snapshot View
        {
            get
            {
                if (_view != null && Time.unscaledTime - _viewAt < 1f) return _view;
                var v = new Snapshot();
                v.ServerExists = TryGetServer(Current, out v.Server);
                if (!v.ServerExists) v.Server = new Slot { Index = Current, Name = GeneratedName, Generated = true };
                v.WorldGenerated = SafeFile.Exists(CurrentWorldFile);
                if (v.WorldGenerated)
                {
                    v.BackTarget = BringBackTarget(out v.BackSame);
                    if (v.BackSame && TryGetSolo(v.BackTarget, out var target)) v.BackName = target.Name;
                }
                v.HasSolo = TryGetSolo(Settings.SourceWorld, out v.Solo);
                _view = v;
                _viewAt = Time.unscaledTime;
                return v;
            }
        }

        public static void Invalidate() => _view = null;

        // ---------------- actions (each returns the message to show) ----------------

        /// <summary>Picks the server world split screen loads next time.</summary>
        public static string Select(int dir)
        {
            if (!NoServerRunning) return L10n.T("world.stopFirst");
            Settings.ServerWorld = NextServer(Current, dir);
            Invalidate();
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
            Invalidate();
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
            // Everything is read before anything is written, so a failure never leaves a half-copied server world.
            var info = ReadInfo(SoloInfo(solo));
            if (info == null) return L10n.T("world.failed");
            info.name = WithoutPrefix(info.name);
            try
            {
                byte[] world = File.ReadAllBytes(SoloWorld(solo));
                string gen = SoloGen(solo);
                byte[] genData = gen != null && SafeFile.Exists(gen) ? File.ReadAllBytes(gen) : null;

                // worldinfo first: until the world file is written, the index only lists as "not generated yet".
                if (!WriteInfo(InfoFile(i), info)) return L10n.T("world.failed");
                if (genData != null) SafeFile.WriteAllBytes(GenFile(i), genData);
                SafeFile.WriteAllBytes(WorldFile(i), world);
                WriteLink(i, solo);
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} world copy failed: {e.Message}");
                return L10n.T("world.failed");
            }
            Settings.ServerWorld = i;
            Invalidate();
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
            string name = WithPrefix(CurrentName);

            if (same && WorldBackup.Save(WorldBackup.BeforeReplace, $"solo world {target} before bringing server world {Current} back",
                    SoloWorld(target), SoloInfo(target)) == null)
                return L10n.T("world.backupFailed"); // never replace a solo world without its backup
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
                WriteLink(Current, target);
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} world copy failed: {e.Message}");
                return L10n.T("world.failed");
            }
            // The game read every worldinfo at startup: refresh this slot so the world list shows it now.
            try { Manager.saves.ReloadWorldInfo(target); } catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} world list refresh: {e.Message}"); }
            Invalidate();
            Debug.Log($"{CouchCoopMod.Tag} server world {Current} ({name}) copied into solo slot {target} (same world: {same})");
            return string.Format(L10n.T("world.broughtBack"), name, target);
        }

        /// <summary>Renames the current server world (applies when the server starts, and when it is brought back).</summary>
        public static string Rename(string name)
        {
            name = WithoutPrefix((name ?? "").Trim()).Trim();
            if (name.Length == 0) return null;
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
            var info = ReadInfo(CurrentInfoFile) ?? new WorldInfo { version = WorldInfo.CURRENT_VERSION, creationDate = new CreationDate(DateTime.Now) };
            info.name = name;
            if (!WriteInfo(CurrentInfoFile, info)) return L10n.T("world.failed");
            Invalidate();
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
