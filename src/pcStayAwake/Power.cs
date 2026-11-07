using System;
using System.Runtime.InteropServices;

namespace PcStayAwake
{
    /// <summary>Kapselt die Windows-APIs, mit denen Standby und Bildschirmabschaltung verhindert werden.</summary>
    internal static class Power
    {
        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);

        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        const uint INPUT_MOUSE = 0;
        const uint MOUSEEVENTF_MOVE = 0x0001;

        /// <summary>Setzt den Wach-Zustand. Muss vom selben Thread aufgerufen werden (UI-Thread).</summary>
        public static void Apply(bool active, AwakeMode mode)
        {
            if (!active)
            {
                SetThreadExecutionState(ES_CONTINUOUS);
                return;
            }
            uint flags = ES_CONTINUOUS | ES_SYSTEM_REQUIRED;
            if (mode == AwakeMode.DisplayAndSystem) flags |= ES_DISPLAY_REQUIRED;
            SetThreadExecutionState(flags);
        }

        /// <summary>
        /// Sendet eine Mausbewegung um 0 Pixel. Der Cursor bewegt sich nicht, aber Windows
        /// setzt den Leerlauf-Timer zurück (Bildschirmschoner, Sperre, "Abwesend"-Status).
        /// </summary>
        public static void SimulateActivity()
        {
            var input = new INPUT[1];
            input[0].type = INPUT_MOUSE;
            input[0].mi.dwFlags = MOUSEEVENTF_MOVE;
            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
