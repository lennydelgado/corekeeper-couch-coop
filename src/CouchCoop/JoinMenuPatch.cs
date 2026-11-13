using HarmonyLib;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// In split screen the Join Game menu opens straight on "Join With IP" with the local server's address,
    /// port and password filled in: the player only has to press Join.
    /// </summary>
    [HarmonyPatch(typeof(RadicalJoinGameMenu), nameof(RadicalJoinGameMenu.Activate))]
    internal static class JoinMenuPatch
    {
        private const int IpEntryId = 1; // dropdown order: 0 = Game ID, 1 = IP

        private static void Postfix(RadicalJoinGameMenu __instance)
        {
            if (!Duo.Active || !Settings.AutoFillJoin) return;
            var info = Duo.ConnectionInfo;
            if (info == null || string.IsNullOrEmpty(info.password)) return;

            try
            {
                var dropdownOption = __instance.GetComponentInChildren<RadicalJoinGameMenu_JoinMethodDropdown>(true);
                var dropdown = dropdownOption != null ? dropdownOption.dropdown : null;
                if (dropdown != null && dropdown.entries != null)
                {
                    foreach (var e in dropdown.entries)
                    {
                        if (e is DropdownEntry entry && entry.entryData != null && entry.entryData.id == IpEntryId)
                        {
                            dropdown.OnEntryClicked(entry); // updates the label and calls ChangeJoinMethod(IP)
                            break;
                        }
                    }
                }
                __instance.ChangeJoinMethod(RadicalJoinGameMenu.JoinMethod.IP);
                __instance.SetSessionData(info.Packed);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{CouchCoopMod.Tag} could not pre-fill Join With IP: {e.Message}");
            }
        }
    }
}
