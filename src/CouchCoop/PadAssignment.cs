using System;
using System.Collections.Generic;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace CouchCoop
{
    /// <summary>
    /// Applies the controller assignment to the game's Rewired players.
    /// The game has a single local player (Rewired player 0) plus the menus' "system player", and attaches
    /// EVERY controller to them. Each window keeps only its own (P1 in the normal window, P2 in the second one).
    /// </summary>
    internal static class PadAssignment
    {
        /// <summary>True while the hub is open: no controller drives the game.</summary>
        public static bool Suspended;
        public static event Action PadsChanged;

        public static IList<Joystick> Joysticks
        {
            get
            {
                try { return ReInput.isReady ? ReInput.controllers.Joysticks : (IList<Joystick>)Array.Empty<Joystick>(); }
                catch { return Array.Empty<Joystick>(); }
            }
        }

        /// <summary>Stable controller id (two identical controllers are told apart by their order).</summary>
        public static string KeyOf(Joystick j)
        {
            string baseKey = $"{j.hardwareTypeGuid}|{j.hardwareIdentifier}";
            int dup = 0;
            foreach (var other in Joysticks)
            {
                if (other == j) break;
                if ($"{other.hardwareTypeGuid}|{other.hardwareIdentifier}" == baseKey) dup++;
            }
            return dup == 0 ? baseKey : $"{baseKey}#{dup}";
        }

        public static bool HasConnectedPadForThisWindow()
        {
            foreach (var j in Joysticks)
                if (PadStore.GetSlot(KeyOf(j)) == Duo.MySlot) return true;
            return false;
        }

        public static bool HasUnknownPad()
        {
            foreach (var j in Joysticks)
                if (PadStore.GetSlot(KeyOf(j)) < 0) return true;
            return false;
        }

        private static bool TryGetPlayers(out Player p0, out Player system)
        {
            p0 = null; system = null;
            try
            {
                var im = Manager.input;
                if (im == null || im.singleplayerInputModule == null) return false;
                p0 = im.singleplayerInputModule.rewiredPlayer;
                system = im.system;
                return p0 != null && system != null;
            }
            catch { return false; }
        }

        public static void Apply(string reason)
        {
            if (!TryGetPlayers(out var p0, out var system)) return;

            // Normal window without a P1 controller chosen: keep the game's original behaviour.
            if (!Suspended && !HasConnectedPadForThisWindow() && !Duo.IsJ2)
            {
                p0.controllers.excludeFromControllerAutoAssignment = false;
                return;
            }

            p0.controllers.excludeFromControllerAutoAssignment = true;
            p0.controllers.ClearControllersOfType<Joystick>();
            system.controllers.ClearControllersOfType<Joystick>();

            if (!Suspended)
            {
                foreach (var j in Joysticks)
                {
                    if (PadStore.GetSlot(KeyOf(j)) != Duo.MySlot) continue; // P2 without a P2 controller takes none
                    p0.controllers.AddController(j, false);
                    system.controllers.AddController(j, false);
                }
            }
            Debug.Log($"{CouchCoopMod.Tag} controllers applied ({reason}) suspended={Suspended} P{Duo.MySlot}={p0.controllers.joystickCount}");
        }

        public static void RestoreGameDefault()
        {
            if (!TryGetPlayers(out var p0, out var system)) return;
            p0.controllers.excludeFromControllerAutoAssignment = false;
            foreach (var j in Joysticks)
            {
                p0.controllers.AddController(j, false);
                system.controllers.AddController(j, false);
            }
        }

        public static void OnPadsChanged()
        {
            Apply("controllers changed");
            PadsChanged?.Invoke();
        }
    }

    // The game re-assigns every controller to player 0 at several points: re-apply right after.
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.AssignJoysticksToSingleplayerInputModule))]
    internal static class AssignJoysticksPatch { private static void Postfix() => PadAssignment.Apply("AssignJoysticks"); }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.SetTouchpadMapsEnabled))]
    internal static class TouchpadMapsPatch { private static void Postfix() => PadAssignment.Apply("SetTouchpadMaps"); }

    [HarmonyPatch(typeof(InputManager), "OnControllerConnected")]
    internal static class ControllerConnectedPatch { private static void Postfix() => PadAssignment.OnPadsChanged(); }

    [HarmonyPatch(typeof(InputManager), "OnControllerDisconnected")]
    internal static class ControllerDisconnectedPatch { private static void Postfix() => PadAssignment.OnPadsChanged(); }
}
