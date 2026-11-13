using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Shared
{
    /// <summary>Runtime environment info (Wine, dedicated server).</summary>
    internal static class Runtime
    {
        private static bool? _wine;
        public static string WineVersion { get; private set; } = "n/a";

        [DllImport("kernel32", CharSet = CharSet.Ansi)] private static extern IntPtr GetModuleHandleA(string name);
        [DllImport("kernel32", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr module, string proc);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr WineGetVersion();

        /// <summary>True under Wine (CrossOver, NotProton, Proton, Whisky...), detected via ntdll!wine_get_version.</summary>
        public static bool IsWine
        {
            get
            {
                if (_wine.HasValue) return _wine.Value;
                _wine = false;
                try
                {
                    IntPtr ntdll = GetModuleHandleA("ntdll.dll");
                    IntPtr fn = ntdll == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(ntdll, "wine_get_version");
                    if (fn != IntPtr.Zero)
                    {
                        _wine = true;
                        try { WineVersion = Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<WineGetVersion>(fn)()) ?? "?"; }
                        catch { WineVersion = "?"; }
                    }
                }
                catch { /* no kernel32: not Wine */ }
                return _wine.Value;
            }
        }

        /// <summary>True inside the dedicated server (CoreKeeperServer.exe, started with -batchmode).</summary>
        public static bool IsDedicatedServer =>
            Application.isBatchMode
            || Application.dataPath.IndexOf("CoreKeeperServer_Data", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
