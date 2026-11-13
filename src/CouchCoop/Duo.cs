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
        public const int Port = 27015;
        public const string Ip = "127.0.0.1";

        public static bool IsJ2 { get; private set; }
        public static bool IsJ2Process => Environment.GetCommandLineArgs()
            .Any(a => a.Equals(P2Arg, StringComparison.OrdinalIgnoreCase) || a.Equals(LegacyP2Arg, StringComparison.OrdinalIgnoreCase));
        public static int MySlot => IsJ2 ? 2 : 1;
        /// <summary>True if this window takes part in split screen.</summary>
        public static bool Active => IsJ2 || _started;

        private static string Pdp => Application.persistentDataPath.Replace('\\', '/').TrimEnd('/');
        private static string ShareDir => Pdp + "/CouchCoop";
        private static string HandoffPath => ShareDir + "/connection.json";
        /// <summary>Local server world. A world from the development builds (.../DuoServer) is reused as is.</summary>
        public static string ServerDataPath =>
            !Directory.Exists(ShareDir + "/Server") && Directory.Exists(Pdp + "/DuoServer") ? Pdp + "/DuoServer" : ShareDir + "/Server";
        private static string ServerDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Core Keeper Dedicated Server"));
        private static string ServerExe => Path.Combine(ServerDir, "CoreKeeperServer.exe");
        public static bool ServerInstalled => File.Exists(ServerExe);

        private static Process _j2Process, _serverProcess;
        private static bool _started, _j2Pending;
        private static float _j2PendingSince = -1f;
        private static DateTime _serverStartedUtc;

        [Serializable]
        public class Connection
        {
            public string ip = Ip;
            public int port = Port;
            public string password = "";
            /// <summary>Format the game's IP field splits by itself: IP;PORT;;PASSWORD</summary>
            public string Packed => $"{ip};{port};;{password}";
        }

        /// <summary>Local server connection info (P1: its own; P2: the one published by P1).</summary>
        public static Connection ConnectionInfo { get; private set; }

        public static void EarlyInit()
        {
            IsJ2 = IsJ2Process;
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
            if (_started && !restartP2 && IsRunning(_j2Process)) return L10n.T("duo.running");

            _started = true;
            Application.runInBackground = true;
            OnGameReady();

            SeedServerWorld();
            ServerAdmin.Ensure(ServerDataPath);
            if (!IsRunning(_serverProcess)) StartServer();
            if (restartP2 || !IsRunning(_j2Process)) _j2Pending = true; // P2 opens once the server is ready

            ConnectionInfo = new Connection { password = Settings.ServerPassword };
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
                return;
            }
            if (!_j2Pending) return;
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

        public static bool ServerWorldExists => SafeFile.Exists(Path.Combine(ServerDataPath, "worlds", "0.world.gzip"));

        public static void CopyConnectionToClipboard()
        {
            var c = ConnectionInfo;
            if (c == null || string.IsNullOrEmpty(c.password)) return;
            try { GUIUtility.systemCopyBuffer = c.Packed; } catch { }
        }

        /// <summary>First start: copies the chosen P1 world into the server (index 0).</summary>
        private static void SeedServerWorld()
        {
            try
            {
                if (ServerWorldExists) return;
                var mainFs = AccessTools.Field(typeof(Manager), "_filesystemInterface")?.GetValue(null) as StandaloneFilesystem;
                if (mainFs == null) return;
                int src = Settings.SourceWorld;
                foreach (var (dir, ext) in new[] { ("worlds", ".world.gzip"), ("worldinfos", ".worldinfo"), ("worldgenparams", ".json") })
                {
                    string from = mainFs.Rel2Abs($"{dir}/{src}{ext}");
                    if (!SafeFile.Exists(from)) continue;
                    Directory.CreateDirectory(Path.Combine(ServerDataPath, dir));
                    File.Copy(from, Path.Combine(ServerDataPath, dir, "0" + ext), true);
                }
                Debug.Log($"{CouchCoopMod.Tag} P1 world {src} copied into the local server");
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
                    "-world 0",
                    "-worldname \"Couch Co-op\"",
                    $"-port {Port}",
                    $"-ip {Ip}",
                    $"-password \"{Settings.ServerPassword}\"",
                    "-maxplayers 4");
                var psi = new ProcessStartInfo(ServerExe, args) { UseShellExecute = false, WorkingDirectory = ServerDir };
                // The server must not pose as the game (environment inherited from Steam).
                foreach (var v in new[] { "SteamAppId", "SteamGameId", "SteamOverlayGameId" }) psi.EnvironmentVariables.Remove(v);

                _serverStartedUtc = DateTime.UtcNow;
                _serverProcess = Process.Start(psi);
                Debug.Log($"{CouchCoopMod.Tag} dedicated server started ({ServerExe})");
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} could not start the dedicated server");
                Debug.LogException(e);
            }
        }

        private static void StopServer()
        {
            if (!IsRunning(_serverProcess)) return;
            try
            {
                // Graceful stop like Launch.ps1 (taskkill without /f): the server saves before quitting.
                Process.Start(new ProcessStartInfo("taskkill.exe", $"/pid {_serverProcess.Id}") { UseShellExecute = false });
                Debug.Log($"{CouchCoopMod.Tag} local server stop requested");
            }
            catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} server stop: {e.Message}"); }
        }

        private static void LaunchJ2Window()
        {
            try
            {
                string exe = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "CoreKeeper.exe"));
                string dir = Path.Combine(Pdp, "J2");
                Directory.CreateDirectory(dir);
                string log = Path.Combine(dir, "Player.log").Replace('/', '\\');
                var passthrough = Environment.GetCommandLineArgs().Skip(1)
                    .Where(a => a.StartsWith("-force-", StringComparison.OrdinalIgnoreCase));
                string args = $"{P2Arg} -popupwindow -screen-fullscreen 0 -logFile \"{log}\" " + string.Join(" ", passthrough);
                _j2Process = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
                Debug.Log($"{CouchCoopMod.Tag} P2 window started");
            }
            catch (Exception e)
            {
                Debug.LogError($"{CouchCoopMod.Tag} could not start the P2 window");
                Debug.LogException(e);
            }
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
            var mainFs = AccessTools.Field(typeof(Manager), "_filesystemInterface")?.GetValue(null) as StandaloneFilesystem;
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
