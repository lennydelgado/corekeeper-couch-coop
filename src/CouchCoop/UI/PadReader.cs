using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace CouchCoop.UI
{
    /// <summary>Raw controller reading (even when not assigned to the game) through Rewired templates.</summary>
    internal struct PadFrame
    {
        public int dx, dy; // -1 / 0 / 1 on edges (one pulse per gesture)
        public bool a, b, x, y, lb, rb, start, select;
        public bool selectStartHeld;
        public bool Any => dx != 0 || dy != 0 || a || b || x || y || lb || rb || start || select;
    }

    internal static class PadReader
    {
        private const float Threshold = 0.6f;
        private static readonly Dictionary<int, Vector2> Prev = new Dictionary<int, Vector2>();

        public static PadFrame Read(Joystick j)
        {
            var f = new PadFrame();
            Vector2 stick;
            var gp = j.GetTemplate<IGamepadTemplate>();
            if (gp != null)
            {
                stick = gp.leftStick.value;
                if (gp.dPad.left.justPressed) f.dx = -1;
                if (gp.dPad.right.justPressed) f.dx = 1;
                if (gp.dPad.up.justPressed) f.dy = 1;
                if (gp.dPad.down.justPressed) f.dy = -1;
                f.a = gp.actionBottomRow1.justPressed;
                f.b = gp.actionBottomRow2.justPressed;
                f.x = gp.actionTopRow1.justPressed;
                f.y = gp.actionTopRow2.justPressed;
                f.lb = gp.leftShoulder1.justPressed;
                f.rb = gp.rightShoulder1.justPressed;
                f.select = gp.center1.justPressed;
                f.start = gp.center2.justPressed;
                f.selectStartHeld = gp.center1.value && gp.center2.value;
            }
            else
            {
                // Controller without a known template: axes 0/1, buttons 0 (A) and 1 (B).
                stick = new Vector2(j.axisCount > 0 ? j.GetAxis(0) : 0f, j.axisCount > 1 ? -j.GetAxis(1) : 0f);
                f.a = j.buttonCount > 0 && j.GetButtonDown(0);
                f.b = j.buttonCount > 1 && j.GetButtonDown(1);
            }

            Prev.TryGetValue(j.id, out var p);
            Prev[j.id] = stick;
            if (f.dx == 0)
            {
                if (stick.x <= -Threshold && p.x > -Threshold) f.dx = -1;
                else if (stick.x >= Threshold && p.x < Threshold) f.dx = 1;
            }
            if (f.dy == 0)
            {
                if (stick.y >= Threshold && p.y < Threshold) f.dy = 1;
                else if (stick.y <= -Threshold && p.y > -Threshold) f.dy = -1;
            }
            return f;
        }

        public static void Buzz(Joystick j, float strength)
        {
            try { if (j.supportsVibration) j.SetVibration(strength, strength, 0.12f, 0.12f); }
            catch { /* some controllers refuse vibration under Wine */ }
        }
    }
}
