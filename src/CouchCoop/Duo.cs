using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using Pug.Platform;
using Rewired;
using Shared;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CouchCoop
{
    /// <summary>
    /// Split screen as "two windows on one screen":
    ///  - the client can only host through Steam relay (not by IP) and P2 uses the same Steam account,
    ///    so we run the official DEDICATED SERVER locally, windowless, with one of P1's worlds;
    ///  - P1 (normal window) and P2 (second instance started with -couchcoop-p2) both join it by IP on
    ///    127.0.0.1: each has their own character, like two players online;
    ///  - P2 has its own save folder (characters, game settings) and its own controller.
    /// </summary>
    internal static class Duo
    {
        public const string P2Arg = "-couchcoop-p2";
        private const string LegacyP2Arg = "-macpad-j2";
        /// <summary>P1's process id, given to P2 so it can close itself (and the server) if P1 is gone.</summary>
        private const string P1PidArg = "-couchcoop-p1pid";
        public const int Port = 27015;
        public const string Ip = "127.0.0.1";

        public static bool IsJ2 { get; private set; }
        public static bool IsJ2Process => Environment.GetCommandLineArgs()
            .Any(a => a.Equals(P2Arg, StringComparison.OrdinalIgnoreCase) || a.Equals(LegacyP2Arg, StringComparison.OrdinalIgnoreCase));
        public static int MySlot => IsJ2 ? 2 : 1;
        /// <summary>True if this window takes part in split screen.</summary>
        public static bool Active => IsJ2 || _started;

        private static string Pdp => Application.persistentDataPath.Replace('\\', '/').TrimEnd('/');
        public static string ShareDir => Pdp + "/CouchCoop";
        private static string HandoffPath => ShareDir + "/connection.json";
        /// <summary>Process id of the server this mod started, to recognise it after a P1 crash.</summary>
        private static string ServerPidPath => ShareDir + "/server.pid";
        /// <summary>Local server world. A world from the development builds (.../DuoServer) is reused as is.</summary>
        public static string ServerDataPath =>
            !Directory.Exists(ShareDir + "/Server") && Directory.Exists(Pdp + "/DuoServer") ? Pdp + "/DuoServer" : ShareDir + "/Server";
        private static string ServerDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Core Keeper Dedicated Server"));
        private static string ServerExe => Path.Combine(ServerDir, "CoreKeeperServer.exe");
        public static bool ServerInstalled => File.Exists(ServerExe);

        private static readonly System.Reflection.FieldInfo MainFsField = AccessTools.Field(typeof(Manager), "_filesystemInterface");

        /// <summary>The game's main save filesystem (created before mods load; P2 redirects it to J2/).</summary>
        public static StandaloneFilesystem MainFilesystem => MainFsField?.GetValue(null) as StandaloneFilesystem;

        private static Process _j2Process, _serverProcess;
        private static bool _started, _j2Pending, _stopping;
        private static float _j2PendingSince = -1f, _oldJ2StopAt = -1f;
        private static DateTime _serverStartedUtc;

        [Serializable]
        public class Connection
        {
            public string ip = Ip;
            public int port = Port;
            public string password = "";
            /// <summary>Local server process id (P2 stops the server if P1 crashed).</summary>
            public int serverPid;
            /// <summary>Format the game's IP field splits by itself: IP;PORT;;PASSWORD</summary>
            public string Packed => $"{ip};{port};;{password}";
        }

        /// <summary>Local server connection info (P1: its own; P2: the one published by P1).</summary>
        public static Connection ConnectionInfo { get; private set; }

        public static void EarlyInit()
        {
            IsJ2 = IsJ2Process;
            if (IsJ2 && !Guardrails.ConsumeP2Token())
            {
                // Only P1 starts P2, once per launch: a P2 window started any other way closes right away.
                Debug.LogError($"{CouchCoopMod.Tag} P2 window not started by P1 (missing or used launch token): closing");
                Application.Quit();
                return;
            }
            if (IsJ2)
            {
                Application.runInBackground = true; // otherwise Unity pauses P2 during its loading screen (no focus)
                RedirectSavesForJ2();
                WindowLayout.Request(WindowLayout.DuoSide(isJ2: true), 1.0f);
            }
            else
            {
                Application.quitting += StopServer;
            }
            Debug.Log($"{CouchCoopMod.Tag} window {(IsJ2 ? "P2" : "P1")}");
        }

        public static void OnGameReady()
        {
            if (!Active) return;
            try { ReInput.configuration.ignoreInputWhenAppNotInFocus = false; }
            catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} focus Rewired : {e.Message}"); }

            if (IsJ2 && Settings.MuteJ2Music)
            {
                // P2 has its own game settings: mute its music (persisted), sound effects stay on.
                try { if (Manager.prefs.musicVolume > 0f) Manager.prefs.musicVolume = 0f; }
                catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} P2 music: {e.Message}"); }
            }
        }

        // ---------------- P1: start ----------------

        public static string Start(bool restartP2 = false)
        {
            if (IsJ2) return L10n.T("duo.isP2");
            if (!ServerInstalled) return L10n.T("duo.noServer");
            string license = Guardrails.CheckLicense();
            if (license != null) return license;
            if (_started && !restartP2 && IsRunning(_j2Process)) return L10n.T("duo.running");
            if (_stopping)
            {
                if (!ServerWorld.NoServerRunning) return L10n.T("duo.stillStopping");
                _stopping = false;
                _serverProcess = null;
            }

            _started = true;
            Application.runInBackground = true;
            OnGameReady();

            SeedServerWorld();
            ServerAdmin.Ensure(ServerDataPath);
            if (!IsRunning(_serverProcess)) AdoptOrphanServer();
            if (!IsRunning(_serverProcess))
            {
                WorldBackup.Save(WorldBackup.Session, $"local server world {ServerWorld.Current} ({ServerWorld.CurrentName}), before a split-screen session",
                    ServerWorld.CurrentWorldFile, ServerWorld.CurrentInfoFile);
                StartServer();
            }
            if (restartP2 && IsRunning(_j2Process))
            {
                // Never two P2 windows: the old one closes first, the new one opens once it is gone (Tick).
                StopGracefully(SafePid(_j2Process), "P2 window");
                _oldJ2StopAt = Time.unscaledTime;
            }
            if (restartP2 || !IsRunning(_j2Process)) _j2Pending = true; // P2 opens once the server is ready

            ConnectionInfo = new Connection { password = Settings.ServerPassword, serverPid = SafePid(_serverProcess) };
            try { SafeFile.WriteAllText(HandoffPath, JsonUtility.ToJson(ConnectionInfo)); }
            catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} connection info not shared: {e.Message}"); }

            WindowLayout.Request(WindowLayout.DuoSide(isJ2: false));
            CopyConnectionToClipboard();
            return L10n.T("duo.started");
        }

        /// <summary>Every frame (P1): opens P2 once the server is ready (or after 3 minutes).</summary>
        public static void Tick()
        {
            if (IsJ2)
            {
                // P2 picks up the info published by P1
                if (ConnectionInfo == null || Time.frameCount % 120 == 0)
                {
                    string raw = SafeFile.ReadAllTextOrNull(HandoffPath);
                    if (raw != null)
                    {
                        try { ConnectionInfo = JsonUtility.FromJson<Connection>(raw); } catch { }
                    }
                }
                WatchP1();
                return;
            }
            if (!_j2Pending) return;
            if (IsRunning(_j2Process))
            {
                // Restart: wait for the old P2 window to close. A window that hangs is forced after 10 s.
                if (_oldJ2StopAt >= 0f && Time.unscaledTime - _oldJ2StopAt > 10f)
                {
                    StopGracefully(SafePid(_j2Process), "P2 window (not answering)", force: true);
                    _oldJ2StopAt = -1f;
                }
                return;
            }
            if (_j2PendingSince < 0f) _j2PendingSince = Time.unscaledTime;
            bool timeout = Time.unscaledTime - _j2PendingSince > 180f;
            if (!ServerReady && !timeout) return;
            _j2Pending = false;
            _j2PendingSince = -1f;
            if (timeout) Debug.LogWarning($"{CouchCoopMod.Tag} server not ready after 3 min, opening P2 anyway");
            LaunchJ2Window();
        }

        public static bool J2Pending => _j2Pending;
        public static bool ServerRunning => IsRunning(_serverProcess);

        /// <summary>True once the server has written its GameInfo.txt since it started.</summary>
        public static bool ServerReady
        {
            get
            {
                try
                {
                    string gi = Path.Combine(ServerDir, "GameInfo.txt");
                    return SafeFile.Exists(gi) && File.GetLastWriteTimeUtc(gi) >= _serverStartedUtc.AddSeconds(-2);
                }
                catch { return false; }
            }
        }

        public static void CopyConnectionToClipboard()
        {
            var c = ConnectionInfo;
            if (c == null || string.IsNullOrEmpty(c.password)) return;
            try { GUIUtility.systemCopyBuffer = c.Packed; } catch { }
        }

        /// <summary>
        /// Very first start (the server has no world at all): copies the chosen solo world into the server.
        /// Without a solo world, the server generates a new one.
        /// </summary>
        private static void SeedServerWorld()
        {
            try
            {
                for (int i = 0; i < ServerWorld.MaxSlots; i++)
                    if (ServerWorld.TryGetServer(i, out _)) return;
                if (!ServerWorld.TryGetSolo(Settings.SourceWorld, out _)) return;
                Debug.Log($"{CouchCoopMod.Tag} first start: {ServerWorld.AddFromSolo(Settings.SourceWorld)}");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private static void StartServer()
        {
            try
            {
                string dp = ServerDataPath;
                Directory.CreateDirectory(dp);
                string log = Path.Combine(dp, "CoreKeeperServerLog.txt").Replace('/', '\\');
                string args = string.Join(" ",
                    "-batchmode",
                    $"-logfile \"{log}\"",
                    $"-datapath \"{dp.Replace('/', '\\')}\"",
                    $"-world {ServerWorld.Current}",
                    $"-worldname \"{ServerWorld.CurrentName.Replace("\"", "'")}\"",
                    $"-port {Port}",
                    $"-ip {Ip}",
                    $"-password \"{Settings.ServerPassword}\"",
                    $"-maxplayers {Guardrails.MaxPlayers}");
                string mode = ServerWorld.PendingMode(ServerWorld.Current);
                if (mode != null) args += $" -worldmode {mode}"; // new world: the server generates it (random seed)
                var psi = new ProcessStartInfo(ServerExe, args) { UseShellExecute = false, WorkingDirectory = ServerDir };
                // The server must not pose as the game (environment inherited from Steam).
                foreach (var v in new[] { "SteamAppId", "SteamGameId", "SteamOverlayGameId" }) psi.EnvironmentVariables.Remove(v);

                _serverStartedUtc = DateTime.UtcNow;
                _serverProcess = Process.Start(psi);
                try { SafeFile.WriteAllText(ServerPidPath, SafePid(_serverProcess).ToString()); }
                catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} server pid not saved: {e.Message}"); }
                Debug.Log($"{CouchCoopMod.Tag} dedicated server started ({ServerExe})");
                Perf.BoostServer(_serverProcess);
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} could not start the dedicated server");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Ends split screen from P1: the server saves and stops, P2 closes, P1 goes back to full screen.
        /// P1 is disconnected from the server and returns to the title screen.
        /// </summary>
        public static string Stop()
        {
            if (IsJ2 || !_started) return L10n.T("duo.notRunning");
            StopServer();
            if (IsRunning(_j2Process)) StopGracefully(SafePid(_j2Process), "P2 window");
            _started = false;
            _stopping = true;
            _j2Pending = false;
            WindowLayout.Request(WindowLayout.Side.Full, 0f);
            Debug.Log($"{CouchCoopMod.Tag} split screen stopped by P1");
            return L10n.T("duo.stopped");
        }

        private static void StopServer()
        {
            if (!IsRunning(_serverProcess)) return;
            StopGracefully(SafePid(_serverProcess), "local server");
        }

        /// <summary>
        /// Asks a process to close like Launch.ps1 does (taskkill without /f): the server saves before quitting,
        /// a game window closes normally. force = /f, only for a window that does not answer.
        /// </summary>
        public static void StopGracefully(int pid, string what, bool force = false)
        {
            if (pid <= 0) return;
            try
            {
                Process.Start(new ProcessStartInfo("taskkill.exe", (force ? "/f " : "") + $"/pid {pid}") { UseShellExecute = false });
                Debug.Log($"{CouchCoopMod.Tag} {what} stop requested (pid {pid}{(force ? ", forced" : "")})");
            }
            catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} {what} stop: {e.Message}"); }
        }

        private static void LaunchJ2Window()
        {
            if (IsRunning(_j2Process)) return; // one P2 window at a time
            _oldJ2StopAt = -1f;
            try
            {
                string exe = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "CoreKeeper.exe"));
                string dir = Path.Combine(Pdp, "J2");
                Directory.CreateDirectory(dir);
                string log = Path.Combine(dir, "Player.log").Replace('/', '\\');
                var passthrough = Environment.GetCommandLineArgs().Skip(1)
                    .Where(a => a.StartsWith("-force-", StringComparison.OrdinalIgnoreCase));
                string args = $"{P2Arg} {Guardrails.TokenArg} {Guardrails.NewP2Token()} {P1PidArg} {SafePid(Process.GetCurrentProcess())} -popupwindow -screen-fullscreen 0 -logFile \"{log}\" " + string.Join(" ", passthrough);
                _j2Process = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
                Debug.Log($"{CouchCoopMod.Tag} P2 window started");
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} could not start the P2 window");
                Debug.LogException(e);
            }
        }

        private static int SafePid(Process p)
        {
            try { return p?.Id ?? 0; } catch { return 0; }
        }

        /// <summary>
        /// The server this mod started in a previous session (pid saved in server.pid), if it still runs.
        /// Any other CoreKeeperServer (a dedicated server the player runs themself) is never touched.
        /// </summary>
        public static Process LeftoverServer()
        {
            try
            {
                if (!int.TryParse(SafeFile.ReadAllTextOrNull(ServerPidPath)?.Trim(), out int pid) || pid <= 0) return null;
                var p = Process.GetProcessById(pid);
                return IsRunning(p) && p.ProcessName.StartsWith("CoreKeeperServer", StringComparison.OrdinalIgnoreCase) ? p : null;
            }
            catch { return null; } // not running anymore
        }

        /// <summary>
        /// The server left running by a crashed session still holds the port and the world:
        /// reuse it instead of starting a second one.
        /// </summary>
        private static void AdoptOrphanServer()
        {
            var p = LeftoverServer();
            if (p == null) return;
            _serverProcess = p;
            _serverStartedUtc = DateTime.MinValue; // its GameInfo.txt is already there
            Debug.Log($"{CouchCoopMod.Tag} reusing the local server left running (pid {p.Id})");
        }

        // ---------------- P2: close with P1 ----------------

        private static int _p1Pid = -1;
        private static float _nextWatch;
        private static bool _closing;

        /// <summary>
        /// P2 only lives alongside P1: when P1 is gone (quit or crash), P2 asks the server to stop (it saves
        /// first) and closes. A normal P1 exit already stops the server; this also covers a crash.
        /// </summary>
        private static void WatchP1()
        {
            if (_closing || Time.unscaledTime < _nextWatch) return;
            _nextWatch = Time.unscaledTime + 2f;
            if (_p1Pid < 0) _p1Pid = ReadP1Pid();
            if (_p1Pid == 0) return; // started by hand or by an old version: nothing to watch
            bool alive;
            try { alive = !Process.GetProcessById(_p1Pid).HasExited; }
            catch (ArgumentException) { alive = false; }
            catch { alive = true; }
            if (alive) return;

            _closing = true;
            Debug.Log($"{CouchCoopMod.Tag} P1 is gone: stopping the local server and closing P2");
            StopGracefully(ConnectionInfo?.serverPid ?? 0, "local server");
            Application.Quit();
        }

        private static int ReadP1Pid()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(P1PidArg, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out int pid)) return pid;
            return 0;
        }

        private static bool IsRunning(Process p)
        {
            if (p == null) return false;
            try { return !p.HasExited; } catch { return true; }
        }

        // ---------------- P2: separate saves ----------------

        /// <summary>
        /// The StandaloneFilesystems are created before mods load, so we rewrite the save root
        /// (.../Core Keeper/Steam/&lt;id&gt; -> .../Core Keeper/J2/Steam/&lt;id&gt;). Mod config is NOT redirected:
        /// P1 and P2 share the CouchCoop settings.
        /// </summary>
        private static void RedirectSavesForJ2()
        {
            var field = AccessTools.Field(typeof(StandaloneFilesystem), "_persistentPath");
            var mainFs = MainFilesystem;
            if (field == null || mainFs == null)
            {
                Debug.LogError($"{CouchCoopMod.Tag} P2: cannot separate saves (field not found), quitting to be safe");
                Application.Quit();
                return;
            }
            string old = ((string)field.GetValue(mainFs)).Replace('\\', '/');
            if (old.Contains("/J2/")) return;
            if (!old.StartsWith(Pdp, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"{CouchCoopMod.Tag} P2: unexpected save path {old}");
                Application.Quit();
                return;
            }
            string redirected = Pdp + "/J2" + old.Substring(Pdp.Length);
            Directory.CreateDirectory(redirected);
            field.SetValue(mainFs, redirected);
            Debug.Log($"{CouchCoopMod.Tag} P2: saves in {redirected}");
        }
    }
}
