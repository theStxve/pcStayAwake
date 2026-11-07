using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PcStayAwake
{
    /// <summary>Einstellungsfenster.</summary>
    internal sealed class SettingsForm : Form
    {
        public event Action<Settings> SettingsApplied;

        readonly Settings settings;
        readonly TableLayoutPanel layout;

        RadioButton modeDisplay, modeSystem;
        CheckBox simulateInput, autostart, notifications;
        ComboBox startBehavior, defaultDuration;

        public SettingsForm(Settings settings)
        {
            this.settings = settings;

            Text = AppInfo.Name + " – Einstellungen";
            Font = SystemFonts.MessageBoxFont;
            Icon = IconArt.CreateIcon(IconVariant.Active);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16, 10, 16, 12);
            BackColor = SystemColors.Window;

            layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Dock = DockStyle.Fill
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(layout);

            BuildModeSection();
            BuildStartSection();
            BuildGeneralSection();
            BuildFooter();

            LoadValues();
        }

        // ---------------- Aufbau ----------------

        void BuildModeSection()
        {
            AddHeader("Modus", false);

            modeDisplay = new RadioButton { Text = "Bildschirm an lassen + PC wach halten", AutoSize = true };
            AddFull(modeDisplay, new Padding(3, 2, 3, 0));
            AddHint("Kein Bildschirmschoner, Bildschirm geht nicht aus, kein Standby.");

            simulateInput = new CheckBox { Text = "Benutzeraktivität simulieren", AutoSize = true };
            AddFull(simulateInput, new Padding(22, 2, 3, 0));
            AddHint("Verhindert zusätzlich die automatische Sperre und den Status „Abwesend“ (z. B. Teams). Der Mauszeiger bewegt sich dabei nicht.", 41);

            modeSystem = new RadioButton { Text = "Nur PC wach halten (Bildschirm darf ausgehen)", AutoSize = true };
            AddFull(modeSystem, new Padding(3, 8, 3, 0));
            AddHint("Der PC geht nicht in den Standby, z. B. für Downloads, Renderings oder Server – der Bildschirm schaltet sich trotzdem normal ab.");

            modeDisplay.CheckedChanged += delegate { simulateInput.Enabled = modeDisplay.Checked; };
        }

        void BuildStartSection()
        {
            AddHeader("Start", true);

            autostart = new CheckBox { Text = "Mit Windows starten", AutoSize = true };
            AddFull(autostart, new Padding(3, 2, 3, 6));

            startBehavior = NewCombo();
            startBehavior.Items.AddRange(new object[] { "Aktiv", "Pausiert", "Letzten Zustand merken" });
            AddRow("Beim Start:", startBehavior);

            defaultDuration = NewCombo();
            foreach (int m in Settings.Durations) defaultDuration.Items.Add(Settings.DurationLabel(m));
            AddRow("Standard-Laufzeit:", defaultDuration);
            AddHint("Gilt beim Start und beim Aktivieren per Linksklick. Danach pausiert pcStayAwake automatisch.");
        }

        void BuildGeneralSection()
        {
            AddHeader("Allgemein", true);
            notifications = new CheckBox { Text = "Benachrichtigungen anzeigen", AutoSize = true };
            AddFull(notifications, new Padding(3, 2, 3, 0));
        }

        void BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 18, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var link = new LinkLabel
            {
                Text = "v" + AppInfo.Version + " · GitHub",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                LinkColor = SystemColors.HotTrack
            };
            link.LinkClicked += delegate
            {
                try { Process.Start(AppInfo.RepoUrl); } catch { }
            };

            var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(88, 0), DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Abbrechen", AutoSize = true, MinimumSize = new Size(88, 0), DialogResult = DialogResult.Cancel };
            ok.Click += delegate { SaveValues(); Close(); };
            cancel.Click += delegate { Close(); };
            AcceptButton = ok;
            CancelButton = cancel;

            footer.Controls.Add(link, 0, 0);
            footer.Controls.Add(ok, 1, 0);
            footer.Controls.Add(cancel, 2, 0);

            layout.Controls.Add(footer);
            layout.SetColumnSpan(footer, 2);
        }

        // ---------------- Werte ----------------

        void LoadValues()
        {
            modeDisplay.Checked = settings.Mode == AwakeMode.DisplayAndSystem;
            modeSystem.Checked = settings.Mode == AwakeMode.SystemOnly;
            simulateInput.Checked = settings.SimulateInput;
            simulateInput.Enabled = modeDisplay.Checked;
            autostart.Checked = SystemIntegration.IsAutostartEnabled();
            startBehavior.SelectedIndex = (int)settings.StartBehavior;
            int idx = Array.IndexOf(Settings.Durations, settings.DefaultDurationMinutes);
            defaultDuration.SelectedIndex = idx < 0 ? 0 : idx;
            notifications.Checked = settings.ShowNotifications;
        }

        void SaveValues()
        {
            settings.Mode = modeSystem.Checked ? AwakeMode.SystemOnly : AwakeMode.DisplayAndSystem;
            settings.SimulateInput = simulateInput.Checked;
            settings.StartBehavior = (StartBehavior)startBehavior.SelectedIndex;
            settings.DefaultDurationMinutes = Settings.Durations[defaultDuration.SelectedIndex];
            settings.ShowNotifications = notifications.Checked;

            try
            {
                if (autostart.Checked != SystemIntegration.IsAutostartEnabled())
                    SystemIntegration.SetAutostart(autostart.Checked, Application.ExecutablePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Autostart konnte nicht geändert werden:\n" + ex.Message,
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            var handler = SettingsApplied;
            if (handler != null) handler(settings);
        }

        // ---------------- Layout-Helfer ----------------

        void AddHeader(string text, bool spaced)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(Font.FontFamily, Font.Size + 2f, FontStyle.Bold),
                ForeColor = Color.FromArgb(37, 99, 235),
                Margin = new Padding(0, spaced ? 16 : 4, 0, 4)
            };
            AddFull(label, label.Margin);
        }

        void AddHint(string text, int indent = 22)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                ForeColor = SystemColors.GrayText
            };
            AddFull(label, new Padding(indent, 0, 3, 2));
        }

        void AddFull(Control c, Padding margin)
        {
            c.Margin = margin;
            layout.Controls.Add(c);
            layout.SetColumnSpan(c, 2);
        }

        void AddRow(string labelText, Control control)
        {
            var label = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 4, 12, 4) };
            control.Margin = new Padding(3, 3, 3, 3);
            layout.Controls.Add(label);
            layout.Controls.Add(control);
        }

        static ComboBox NewCombo()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        }
    }
}
