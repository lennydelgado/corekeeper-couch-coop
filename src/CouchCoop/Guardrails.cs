using System;
using HarmonyLib;
using Shared;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Limits that keep split screen a local, same-couch feature (agreed with Pugstorm):
    ///  - the local server accepts 2 players and only listens on 127.0.0.1 (see Duo.StartServer);
    ///  - split screen needs a Steam copy of the game on this account;
    ///  - P2 can only be started by P1, once per launch (one-time token), one window at a time (a restart closes
    ///    the old one first), and closes when P1 is gone (Duo);
    ///  - P2 can only join the local server: no solo game, no hosting, no other server.
    /// </summary>
    internal static class Guardrails
    {
        public const string TokenArg = "-couchcoop-token";
        public const int MaxPlayers = 2;

        private static string TokenPath => Duo.ShareDir + "/p2-launch.txt";

        // ---------------- license ----------------

        /// <summary>True if this Steam account owns the game. Null message when OK.</summary>
        public static string CheckLicense()
        {
            try
            {
                if (!Steamworks.SteamClient.IsValid) return L10n.T("guard.noSteam");
                if (!Steamworks.SteamApps.IsSubscribed) return L10n.T("guard.noLicense");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{CouchCoopMod.Tag} license check failed: {e.Message}");
                return L10n.T("guard.noSteam");
            }
        }

        // ---------------- one-time P2 token ----------------

        /// <summary>P1: new token for the P2 window about to start (replaces any unused one).</summary>
        public static string NewP2Token()
        {
            string token = Guid.NewGuid().ToString("N");
            SafeFile.WriteAllText(TokenPath, token);
            return token;
        }

        /// <summary>P2, at startup: true if P1 issued this token. The token is used up either way.</summary>
        public static bool ConsumeP2Token()
        {
            string given = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(TokenArg, StringComparison.OrdinalIgnoreCase)) given = args[i + 1];

            string expected = SafeFile.ReadAllTextOrNull(TokenPath)?.Trim();
            bool ok = !string.IsNullOrEmpty(given) && !string.IsNullOrEmpty(expected) && given == expected;
            try { SafeFile.WriteAllText(TokenPath, ""); } catch { }
            return ok;
        }

        // ---------------- P2: local server only ----------------

        public static bool IsLocalServer(ServerConnectionInfo info) =>
            info.JoinedWithIP
            && (info.PublicIP == Duo.Ip || string.Equals(info.PublicIP, "localhost", StringComparison.OrdinalIgnoreCase))
            && info.Port == Duo.Port.ToString();
    }

    /// <summary>P2 can only connect to the local server (Join With IP, 127.0.0.1:27015), never by Game ID or invite.</summary>
    [HarmonyPatch(typeof(NetworkingManager), nameof(NetworkingManager.Connect))]
    internal static class P2ConnectPatch
    {
        private static bool Prefix(ServerConnectionInfo connectionInfo, Action<bool> callback)
        {
            if (!Duo.IsJ2 || Guardrails.IsLocalServer(connectionInfo)) return true;
            Debug.Log($"{CouchCoopMod.Tag} P2: connection refused (not the local server)");
            UI.Hub.Notify(L10n.T("guard.p2LocalOnly"), 6f);
            try { callback?.Invoke(false); } catch { }
            return false;
        }
    }

    /// <summary>P2 cannot start a solo game (which would also host it): its "Play" entry only shows a message.</summary>
    [HarmonyPatch(typeof(RadicalMainMenuOption_Singleplayer), nameof(RadicalMainMenuOption_Singleplayer.OnActivated))]
    internal static class P2SingleplayerPatch
    {
        private static bool Prefix()
        {
            if (!Duo.IsJ2) return true;
            UI.Hub.Notify(L10n.T("guard.p2NoSolo"), 6f);
            return false;
        }
    }

    /// <summary>Safety net: P2 never loads a world of its own (worldId != -1 means hosting one).</summary>
    [HarmonyPatch(typeof(ECSManager), nameof(ECSManager.StartEcs))]
    internal static class P2StartEcsPatch
    {
        private static bool Prefix(int worldId, Action<bool> onEcsStartCompete)
        {
            if (!Duo.IsJ2 || worldId == -1) return true;
            Debug.Log($"{CouchCoopMod.Tag} P2: world {worldId} not loaded (P2 only joins the local server)");
            UI.Hub.Notify(L10n.T("guard.p2NoSolo"), 6f);
            try { onEcsStartCompete?.Invoke(false); } catch { }
            return false;
        }
    }
}
