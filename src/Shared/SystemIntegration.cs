using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace PcStayAwake
{
    /// <summary>Autostart, Verknüpfungen, Deinstallations-Eintrag und Steuerung laufender Instanzen.</summary>
    internal static class SystemIntegration
    {
        // ---------- Autostart ----------

        public static bool IsAutostartEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AppInfo.RunKey, false))
                return key != null && key.GetValue(AppInfo.Name) != null;
        }

        public static void SetAutostart(bool enabled, string exePath)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(AppInfo.RunKey))
            {
                if (key == null) return;
                if (enabled)
                    key.SetValue(AppInfo.Name, "\"" + exePath + "\" " + AppInfo.AutostartArg);
                else
                    key.DeleteValue(AppInfo.Name, false);
            }
        }

        public static void RemoveLegacyAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AppInfo.RunKey, true))
                    if (key != null) key.DeleteValue(AppInfo.LegacyRunValue, false);
            }
            catch { }
        }

        // ---------- Verknüpfungen ----------

        public static void CreateShortcut(string linkPath, string targetPath, string description)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object link = null;
            try
            {
                link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
                Type lt = link.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { targetPath });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(targetPath) });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { description });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { targetPath + ",0" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally
            {
                if (link != null) Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
        }

        public static void DeleteFileQuietly(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // ---------- Apps & Features ----------

        public static bool IsInstalled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AppInfo.UninstallKey, false))
                return key != null;
        }

        public static void RegisterUninstallEntry(string exePath, long sizeBytes)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(AppInfo.UninstallKey))
            {
                key.SetValue("DisplayName", AppInfo.Name);
                key.SetValue("DisplayVersion", AppInfo.Version);
                key.SetValue("Publisher", AppInfo.Publisher);
                key.SetValue("DisplayIcon", exePath + ",0");
                key.SetValue("InstallLocation", Path.GetDirectoryName(exePath));
                key.SetValue("UninstallString", "\"" + exePath + "\" " + AppInfo.UninstallArg);
                key.SetValue("URLInfoAbout", AppInfo.RepoUrl);
                key.SetValue("HelpLink", AppInfo.RepoUrl);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("EstimatedSize", (int)Math.Max(1, sizeBytes / 1024), RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        public static void RemoveUninstallEntry()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(AppInfo.UninstallKey, false); } catch { }
        }

        // ---------- Laufende Instanz ----------

        /// <summary>Bittet eine laufende Instanz, sich sauber zu beenden; erzwingt es notfalls.</summary>
        public static void CloseRunningInstance(int timeoutMs)
        {
            try
            {
                EventWaitHandle exitEvent;
                if (EventWaitHandle.TryOpenExisting(AppInfo.ExitEventName, out exitEvent))
                {
                    exitEvent.Set();
                    exitEvent.Dispose();
                }
            }
            catch { }

            int self = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppInfo.ExeName)))
            {
                try
                {
                    if (p.Id == self) continue;
                    if (!p.WaitForExit(timeoutMs)) { p.Kill(); p.WaitForExit(2000); }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
    }
}
