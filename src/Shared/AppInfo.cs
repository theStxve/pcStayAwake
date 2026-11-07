using System;
using System.IO;

namespace PcStayAwake
{
    /// <summary>Zentrale Konstanten, die von App und Setup gemeinsam genutzt werden.</summary>
    internal static class AppInfo
    {
        public const string Name = "pcStayAwake";
        public const string Version = "1.0.0";
        public const string Publisher = "theStxve";
        public const string RepoUrl = "https://github.com/theStxve/pcStayAwake";
        public const string ExeName = "pcStayAwake.exe";
        public const string Description = "Hält den PC und optional den Bildschirm wach.";

        public const string MutexName = "pcStayAwake_SingleInstance";
        public const string ShowEventName = "pcStayAwake_ShowSettings";
        public const string ExitEventName = "pcStayAwake_Exit";

        public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string SettingsKey = @"Software\pcStayAwake";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\pcStayAwake";

        /// <summary>Name des Autostart-Eintrags aus der allerersten Version (wird automatisch entfernt).</summary>
        public const string LegacyRunValue = "WakeScreen";

        public const string AutostartArg = "--autostart";
        public const string UninstallArg = "--uninstall";

        public static string InstallDir
        {
            get
            {
                return Path.Combine(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), Name);
            }
        }

        public static string InstalledExePath
        {
            get { return Path.Combine(InstallDir, ExeName); }
        }

        public static string StartMenuShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Name + ".lnk"); }
        }

        public static string DesktopShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Name + ".lnk"); }
        }
    }
}
