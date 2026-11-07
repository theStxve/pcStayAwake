using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PcStayAwake
{
    /// <summary>Tray-Icon, Menü und die eigentliche Wach-Logik.</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        const int KeepAliveIntervalMs = 50000;

        readonly Settings settings;
        readonly NotifyIcon tray;
        readonly Control invoker;
        readonly System.Windows.Forms.Timer keepAliveTimer;
        readonly System.Windows.Forms.Timer countdownTimer;
        readonly EventWaitHandle showEvent;
        readonly EventWaitHandle exitEvent;
        readonly Icon iconActive, iconSystemOnly, iconPaused;

        readonly ToolStripMenuItem statusItem, toggleItem, durationMenu, modeDisplayItem, modeSystemItem;

        SettingsForm settingsForm;
        bool active;
        DateTime? endTime;
        bool exiting;

        public TrayApp(bool launchedByAutostart)
        {
            settings = Settings.Load();

            invoker = new Control();
            IntPtr forceHandle = invoker.Handle; // Handle erzeugen, damit BeginInvoke funktioniert

            Size iconSize = SystemInformation.SmallIconSize;
            iconActive = new Icon(IconArt.CreateIcon(IconVariant.Active), iconSize);
            iconSystemOnly = new Icon(IconArt.CreateIcon(IconVariant.SystemOnly), iconSize);
            iconPaused = new Icon(IconArt.CreateIcon(IconVariant.Paused), iconSize);

            // ---- Menü ----
            var menu = new ContextMenuStrip();
            statusItem = new ToolStripMenuItem("Status") { Enabled = false };
            toggleItem = new ToolStripMenuItem("Pausieren", null, delegate { Toggle(); });
            toggleItem.Font = new Font(toggleItem.Font, FontStyle.Bold);

            durationMenu = new ToolStripMenuItem("Aktiv für");
            foreach (int minutes in Settings.Durations)
            {
                int m = minutes;
                var item = new ToolStripMenuItem(Settings.DurationLabel(m), null, delegate { SetActive(true, m); });
                item.Tag = m;
                durationMenu.DropDownItems.Add(item);
            }

            var modeMenu = new ToolStripMenuItem("Modus");
            modeDisplayItem = new ToolStripMenuItem("Bildschirm an + PC wach", null, delegate { SetMode(AwakeMode.DisplayAndSystem); });
            modeSystemItem = new ToolStripMenuItem("Nur PC wach (Bildschirm darf aus)", null, delegate { SetMode(AwakeMode.SystemOnly); });
            modeMenu.DropDownItems.Add(modeDisplayItem);
            modeMenu.DropDownItems.Add(modeSystemItem);

            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(toggleItem);
            menu.Items.Add(durationMenu);
            menu.Items.Add(modeMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Einstellungen…", null, delegate { ShowSettings(); }));
            menu.Items.Add(new ToolStripMenuItem("Beenden", null, delegate { ExitApp(); }));

            tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true, Icon = iconPaused };
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Toggle(); };

            // ---- Timer ----
            keepAliveTimer = new System.Windows.Forms.Timer { Interval = KeepAliveIntervalMs };
            keepAliveTimer.Tick += delegate { KeepAlive(); };

            countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            countdownTimer.Tick += delegate { OnCountdown(); };
            countdownTimer.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // ---- Signale anderer Instanzen (zweiter Start / Setup / Deinstallation) ----
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.ShowEventName);
            exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.ExitEventName);
            var listener = new Thread(ListenForSignals) { IsBackground = true, Name = "SignalListener" };
            listener.Start();

            // ---- Startzustand ----
            bool startActive = settings.StartBehavior == StartBehavior.Active
                || (settings.StartBehavior == StartBehavior.RememberLast && settings.LastActive);
            SetActive(startActive, settings.DefaultDurationMinutes);

            if (!launchedByAutostart)
                Notify(startActive ? "pcStayAwake läuft" : "pcStayAwake ist pausiert",
                    "Linksklick aufs Tray-Icon schaltet an/aus, Rechtsklick öffnet das Menü.");
        }

        // ================= Logik =================

        void Toggle()
        {
            SetActive(!active, settings.DefaultDurationMinutes);
        }

        void SetActive(bool on, int minutes)
        {
            active = on;
            endTime = (on && minutes > 0) ? DateTime.Now.AddMinutes(minutes) : (DateTime?)null;

            Power.Apply(active, settings.Mode);
            if (active)
            {
                KeepAlive();
                keepAliveTimer.Start();
            }
            else
            {
                keepAliveTimer.Stop();
            }

            if (settings.LastActive != active)
            {
                settings.LastActive = active;
                settings.Save();
            }

            foreach (ToolStripItem item in durationMenu.DropDownItems)
            {
                var mi = item as ToolStripMenuItem;
                if (mi != null) mi.Checked = active && (int)mi.Tag == (endTime.HasValue ? minutes : 0);
            }
            UpdateUi();
        }

        void SetMode(AwakeMode mode)
        {
            settings.Mode = mode;
            settings.Save();
            Power.Apply(active, settings.Mode);
            UpdateUi();
        }

        void KeepAlive()
        {
            if (!active) return;
            // Zustand regelmäßig erneut setzen – robust gegen Treiber/Tools, die ihn zurücksetzen.
            Power.Apply(true, settings.Mode);
            if (settings.Mode == AwakeMode.DisplayAndSystem && settings.SimulateInput)
                Power.SimulateActivity();
        }

        void OnCountdown()
        {
            if (!active || !endTime.HasValue) return;
            if (DateTime.Now >= endTime.Value)
            {
                SetActive(false, 0);
                Notify("pcStayAwake pausiert", "Die eingestellte Zeit ist abgelaufen.");
                return;
            }
            UpdateUi();
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
                invoker.BeginInvoke((MethodInvoker)delegate { if (active) KeepAlive(); });
        }

        void ApplySettings(Settings updated)
        {
            settings.Mode = updated.Mode;
            settings.SimulateInput = updated.SimulateInput;
            settings.StartBehavior = updated.StartBehavior;
            settings.DefaultDurationMinutes = updated.DefaultDurationMinutes;
            settings.ShowNotifications = updated.ShowNotifications;
            settings.Save();
            Power.Apply(active, settings.Mode);
            if (active) KeepAlive();
            UpdateUi();
        }

        // ================= UI =================

        void UpdateUi()
        {
            string status;
            if (!active) status = "Pausiert";
            else
            {
                string what = settings.Mode == AwakeMode.DisplayAndSystem ? "Bildschirm + PC" : "Nur PC";
                if (endTime.HasValue)
                {
                    TimeSpan left = endTime.Value - DateTime.Now;
                    if (left < TimeSpan.Zero) left = TimeSpan.Zero;
                    status = string.Format("Aktiv ({0}) – noch {1}:{2:00}:{3:00}",
                        what, (int)left.TotalHours, left.Minutes, left.Seconds);
                }
                else status = "Aktiv (" + what + ") – unbegrenzt";
            }

            statusItem.Text = status;
            toggleItem.Text = active ? "Pausieren" : "Aktivieren";
            modeDisplayItem.Checked = settings.Mode == AwakeMode.DisplayAndSystem;
            modeSystemItem.Checked = settings.Mode == AwakeMode.SystemOnly;

            tray.Icon = !active ? iconPaused
                : settings.Mode == AwakeMode.DisplayAndSystem ? iconActive : iconSystemOnly;
            string tip = AppInfo.Name + ": " + status;
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        void Notify(string title, string text)
        {
            if (settings.ShowNotifications)
                tray.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
        }

        void ShowSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                if (settingsForm.WindowState == FormWindowState.Minimized)
                    settingsForm.WindowState = FormWindowState.Normal;
                settingsForm.Activate();
                return;
            }
            settingsForm = new SettingsForm(settings.Clone());
            settingsForm.SettingsApplied += ApplySettings;
            settingsForm.FormClosed += delegate { settingsForm = null; };
            settingsForm.Show();
            settingsForm.Activate();
        }

        // ================= Lebenszyklus =================

        void ListenForSignals()
        {
            var handles = new WaitHandle[] { showEvent, exitEvent };
            while (true)
            {
                int idx = WaitHandle.WaitAny(handles);
                if (idx == 0)
                    invoker.BeginInvoke((MethodInvoker)ShowSettings);
                else
                {
                    invoker.BeginInvoke((MethodInvoker)ExitApp);
                    return;
                }
            }
        }

        void ExitApp()
        {
            if (exiting) return;
            exiting = true;

            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Close();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            keepAliveTimer.Stop();
            countdownTimer.Stop();
            Power.Apply(false, settings.Mode);

            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }
}
