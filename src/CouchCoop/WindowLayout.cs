using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CouchCoop
{
    /// <summary>
    /// Snaps the game window, borderless, to one half of the screen it is on (Win32; under Wine the windows
    /// are real macOS/X11 windows, so it also works on a TV over HDMI).
    /// </summary>
    internal static class WindowLayout
    {
        public enum Side { None, Left, Right, Top, Bottom, Full }

        private static Side _pending = Side.None;
        private static float _applyAt;
        private static int _step, _w, _h, _x, _y;
        public static Side Current { get; private set; } = Side.None;

        public static bool IsSplit(Side s) => s == Side.Left || s == Side.Right || s == Side.Top || s == Side.Bottom;

        /// <summary>This window's half according to the settings (P1 bottom / left, P2 top / right).</summary>
        public static Side DuoSide(bool isJ2) => Settings.SideBySide
            ? (isJ2 ? Side.Right : Side.Left)
            : (isJ2 ? Side.Top : Side.Bottom);

        public static void Request(Side side, float delay = 0.5f)
        {
            _pending = side;
            _applyAt = Time.unscaledTime + delay;
            _step = 0;
        }

        public static void Toggle()
        {
            Request(IsSplit(Current) ? Side.Full : DuoSide(Duo.IsJ2), 0f);
        }

        /// <summary>Every frame: SetResolution first, then style + position once Unity has resized.</summary>
        public static void Tick()
        {
            if (_pending == Side.None || Time.unscaledTime < _applyAt) return;
            IntPtr hwnd = FindOwnWindow();
            if (hwnd == IntPtr.Zero) { _applyAt = Time.unscaledTime + 1f; return; }
            if (!GetMonitorRect(hwnd, out var mon)) { _pending = Side.None; return; }
            int monW = mon.right - mon.left, monH = mon.bottom - mon.top;

            if (_pending == Side.Full)
            {
                SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(WS_OVERLAPPEDWINDOW | WS_VISIBLE));
                Screen.SetResolution(monW, monH, FullScreenMode.FullScreenWindow);
                Current = Side.Full;
                _pending = Side.None;
                return;
            }

            if (_step == 0)
            {
                switch (_pending)
                {
                    case Side.Left: _w = monW / 2; _h = monH; _x = mon.left; _y = mon.top; break;
                    case Side.Right: _w = monW / 2; _h = monH; _x = mon.left + monW / 2; _y = mon.top; break;
                    case Side.Top: _w = monW; _h = monH / 2; _x = mon.left; _y = mon.top; break;
                    default: _w = monW; _h = monH / 2; _x = mon.left; _y = mon.top + monH / 2; break;
                }
                Screen.SetResolution(_w, _h, FullScreenMode.Windowed);
                _step = 1;
                _applyAt = Time.unscaledTime + 0.4f;
                return;
            }

            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(WS_POPUP | WS_VISIBLE));
            SetWindowPos(hwnd, IntPtr.Zero, _x, _y, _w, _h, SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
            Debug.Log($"{CouchCoopMod.Tag} window {_pending}: {_x},{_y} {_w}x{_h} (screen {monW}x{monH})");
            Diagnostics.Schedule(1.5f);
            Current = _pending;
            _pending = Side.None;
        }

        // ---------------- Win32 ----------------

        private const int GWL_STYLE = -16;
        private const long WS_POPUP = 0x80000000L, WS_VISIBLE = 0x10000000L, WS_OVERLAPPEDWINDOW = 0x00CF0000L;
        private const uint SWP_NOZORDER = 0x0004, SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040, MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        private static readonly EnumWindowsProc Callback = OnEnum; // kept alive for the GC
        private static IntPtr _found;
        private static uint _pid;

        [DllImport("user32")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("user32", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int idx, IntPtr value);
        [DllImport("user32")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfoW(IntPtr mon, ref MONITORINFO info);

        private static bool OnEnum(IntPtr hwnd, IntPtr _)
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != _pid || !IsWindowVisible(hwnd)) return true;
            var sb = new StringBuilder(64);
            GetClassNameW(hwnd, sb, sb.Capacity);
            if (sb.ToString() != "UnityWndClass") return true;
            _found = hwnd;
            return false;
        }

        private static IntPtr FindOwnWindow()
        {
            try
            {
                _found = IntPtr.Zero;
                _pid = (uint)Process.GetCurrentProcess().Id;
                EnumWindows(Callback, IntPtr.Zero);
                return _found;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{CouchCoopMod.Tag} window not found: {e.Message}");
                return IntPtr.Zero;
            }
        }

        private static bool GetMonitorRect(IntPtr hwnd, out RECT rect)
        {
            rect = default;
            try
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info)) return false;
                rect = info.rcMonitor;
                return true;
            }
            catch { return false; }
        }
    }
}
