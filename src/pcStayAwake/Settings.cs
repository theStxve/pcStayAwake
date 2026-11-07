using System;
using Microsoft.Win32;

namespace PcStayAwake
{
    internal enum AwakeMode
    {
        /// <summary>Bildschirm bleibt an, PC geht nicht in den Standby.</summary>
        DisplayAndSystem = 0,
        /// <summary>Nur der PC bleibt wach, der Bildschirm darf ausgehen.</summary>
        SystemOnly = 1
    }

    internal enum StartBehavior
    {
        Active = 0,
        Paused = 1,
        RememberLast = 2
    }

    /// <summary>Benutzereinstellungen, gespeichert unter HKCU\Software\pcStayAwake.</summary>
    internal sealed class Settings
    {
        public static readonly int[] Durations = { 0, 15, 30, 60, 120, 240, 480 };

        public AwakeMode Mode = AwakeMode.DisplayAndSystem;
        public bool SimulateInput = true;
        public StartBehavior StartBehavior = StartBehavior.Active;
        public bool LastActive = true;
        public int DefaultDurationMinutes = 0;
        public bool ShowNotifications = true;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AppInfo.SettingsKey, false))
                {
                    if (key == null) return s;
                    s.Mode = (AwakeMode)Clamp(ReadInt(key, "Mode", 0), 0, 1);
                    s.SimulateInput = ReadInt(key, "SimulateInput", 1) != 0;
                    s.StartBehavior = (StartBehavior)Clamp(ReadInt(key, "StartBehavior", 0), 0, 2);
                    s.LastActive = ReadInt(key, "LastActive", 1) != 0;
                    s.DefaultDurationMinutes = ReadInt(key, "DefaultDuration", 0);
                    if (Array.IndexOf(Durations, s.DefaultDurationMinutes) < 0) s.DefaultDurationMinutes = 0;
                    s.ShowNotifications = ReadInt(key, "ShowNotifications", 1) != 0;
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(AppInfo.SettingsKey))
                {
                    key.SetValue("Mode", (int)Mode, RegistryValueKind.DWord);
                    key.SetValue("SimulateInput", SimulateInput ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("StartBehavior", (int)StartBehavior, RegistryValueKind.DWord);
                    key.SetValue("LastActive", LastActive ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("DefaultDuration", DefaultDurationMinutes, RegistryValueKind.DWord);
                    key.SetValue("ShowNotifications", ShowNotifications ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        public Settings Clone()
        {
            return (Settings)MemberwiseClone();
        }

        public static string DurationLabel(int minutes)
        {
            if (minutes <= 0) return "Unbegrenzt";
            if (minutes < 60) return minutes + " Minuten";
            int h = minutes / 60;
            return h == 1 ? "1 Stunde" : h + " Stunden";
        }

        static int ReadInt(RegistryKey key, string name, int fallback)
        {
            object v = key.GetValue(name);
            return v is int ? (int)v : fallback;
        }

        static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }
}
