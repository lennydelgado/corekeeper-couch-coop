using System;
using System.IO;
using System.Linq;
using Shared;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Makes both players admins of the local server. The server matches admins by Steam ID and P1 and P2
    /// share the same account, so one entry (privileges = 2: permanent admin) is enough.
    /// Written to &lt;datapath&gt;/Admins.json BEFORE the server starts (it reloads and de-duplicates the file).
    /// </summary>
    internal static class ServerAdmin
    {
        private const int OwnerPrivileges = 2;

        public static void Ensure(string serverDataPath)
        {
            ulong steamId;
            try { steamId = Steamworks.SteamClient.SteamId.Value; }
            catch (Exception e) { Debug.LogWarning($"{CouchCoopMod.Tag} Steam ID unavailable, admin not added: {e.Message}"); return; }
            if (steamId == 0) return;

            string path = Path.Combine(serverDataPath, "Admins.json");
            try
            {
                var list = new AdminList();
                string raw = SafeFile.ReadAllTextOrNull(path);
                if (raw != null) JsonUtility.FromJsonOverwrite(raw, list);
                if (list.adminList == null) list.adminList = new System.Collections.Generic.List<PlayerAdminEntry>();

                if (list.adminList.Any(a => a.steamId == steamId && a.privileges >= OwnerPrivileges)) return;

                list.adminList.RemoveAll(a => a.steamId == steamId); // older, lower level
                int index = list.adminList.Count == 0 ? 1 : list.adminList.Max(a => a.index) + 1;
                var entry = new PlayerAdminEntry { index = index, privileges = OwnerPrivileges, steamId = steamId };
                entry.Name = "CouchCoop";
                list.adminList.Add(entry);

                SafeFile.WriteAllText(path, JsonUtility.ToJson(list, true));
                Debug.Log($"{CouchCoopMod.Tag} P1 and P2 added as admins of the local server");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{CouchCoopMod.Tag} Admins.json not updated: {e.Message}");
            }
        }
    }
}
