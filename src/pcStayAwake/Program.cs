using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("pcStayAwake")]
[assembly: AssemblyDescription("Hält den PC und optional den Bildschirm wach.")]
[assembly: AssemblyProduct("pcStayAwake")]
[assembly: AssemblyCompany("theStxve")]
[assembly: AssemblyCopyright("Copyright © 2026 theStxve")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

namespace PcStayAwake
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (HasArg(args, AppInfo.UninstallArg))
                return Uninstall() ? 0 : 1;

            bool createdNew;
            using (var mutex = new Mutex(true, AppInfo.MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Läuft schon -> Einstellungen der laufenden Instanz öffnen.
                    EventWaitHandle show;
                    if (EventWaitHandle.TryOpenExisting(AppInfo.ShowEventName, out show))
                    {
                        show.Set();
                        show.Dispose();
                    }
                    return 0;
                }

                SystemIntegration.RemoveLegacyAutostart();
                Application.Run(new TrayApp(HasArg(args, AppInfo.AutostartArg)));
                GC.KeepAlive(mutex);
            }
            return 0;
        }

        static bool HasArg(string[] args, string name)
        {
            foreach (string a in args)
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Wird von "Apps & Features" über den Uninstall-Eintrag aufgerufen.</summary>
        static bool Uninstall()
        {
            var answer = MessageBox.Show(
                AppInfo.Name + " wirklich deinstallieren?\n\nEinstellungen, Verknüpfungen und Autostart werden ebenfalls entfernt.",
                AppInfo.Name + " deinstallieren", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return false;

            SystemIntegration.CloseRunningInstance(5000);
            try { SystemIntegration.SetAutostart(false, ""); } catch { }
            SystemIntegration.RemoveLegacyAutostart();
            SystemIntegration.DeleteFileQuietly(AppInfo.StartMenuShortcut);
            SystemIntegration.DeleteFileQuietly(AppInfo.DesktopShortcut);
            try { Registry.CurrentUser.DeleteSubKeyTree(AppInfo.SettingsKey, false); } catch { }
            SystemIntegration.RemoveUninstallEntry();

            // Programmordner erst löschen, wenn dieser Prozess beendet ist.
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath).TrimEnd('\\');
            if (string.Equals(exeDir, AppInfo.InstallDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.SetCurrentDirectory(Path.GetTempPath());
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        WorkingDirectory = Path.GetTempPath(),
                        Arguments = "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + exeDir + "\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
                catch { }
            }

            MessageBox.Show(AppInfo.Name + " wurde deinstalliert.", AppInfo.Name,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
    }
}
