using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("pcStayAwake Setup")]
[assembly: AssemblyDescription("Installiert pcStayAwake für den aktuellen Benutzer.")]
[assembly: AssemblyProduct("pcStayAwake")]
[assembly: AssemblyCompany("theStxve")]
[assembly: AssemblyCopyright("Copyright © 2026 theStxve")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

namespace PcStayAwake.Setup
{
    internal static class SetupProgram
    {
        public const string PayloadName = "pcStayAwake.exe.payload";

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    internal sealed class SetupForm : Form
    {
        readonly CheckBox desktopShortcut, autostart, launchAfter;
        readonly Button installButton, cancelButton;
        readonly Label statusLabel;
        readonly bool isUpdate;

        public SetupForm()
        {
            isUpdate = SystemIntegration.IsInstalled() || File.Exists(AppInfo.InstalledExePath);

            Text = AppInfo.Name + " Setup";
            Font = SystemFonts.MessageBoxFont;
            Icon = IconArt.CreateIcon(IconVariant.Active);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = SystemColors.Window;
            Padding = new Padding(20, 16, 20, 14);

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Dock = DockStyle.Fill
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(layout);

            // Kopfbereich: Icon + Titel
            var logo = new PictureBox
            {
                Image = IconArt.Render(64, IconVariant.Active),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(56, 56),
                Margin = new Padding(0, 0, 14, 0)
            };
            var titleBox = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Anchor = AnchorStyles.Left
            };
            titleBox.Controls.Add(new Label
            {
                Text = AppInfo.Name + " " + AppInfo.Version + (isUpdate ? " aktualisieren" : " installieren"),
                AutoSize = true,
                Font = new Font(Font.FontFamily, Font.Size + 4f, FontStyle.Bold),
                Margin = new Padding(0, 4, 0, 2)
            });
            titleBox.Controls.Add(new Label
            {
                Text = AppInfo.Description,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(1, 0, 0, 0)
            });
            layout.Controls.Add(logo, 0, 0);
            layout.Controls.Add(titleBox, 1, 0);

            var pathLabel = new Label
            {
                Text = "Installationsordner (nur für dich, keine Adminrechte nötig):\n" + AppInfo.InstallDir,
                AutoSize = true,
                MaximumSize = new Size(440, 0),
                Margin = new Padding(0, 18, 0, 10)
            };
            AddFull(layout, pathLabel);

            var startMenuInfo = new Label
            {
                Text = "✓  Eintrag im Startmenü wird erstellt",
                AutoSize = true,
                Margin = new Padding(3, 0, 0, 4)
            };
            AddFull(layout, startMenuInfo);

            desktopShortcut = new CheckBox { Text = "Verknüpfung auf dem Desktop erstellen", AutoSize = true, Checked = File.Exists(AppInfo.DesktopShortcut) };
            autostart = new CheckBox { Text = "Mit Windows starten", AutoSize = true, Checked = !isUpdate || SystemIntegration.IsAutostartEnabled() };
            launchAfter = new CheckBox { Text = AppInfo.Name + " nach der Installation starten", AutoSize = true, Checked = true };
            AddFull(layout, desktopShortcut);
            AddFull(layout, autostart);
            AddFull(layout, launchAfter);

            statusLabel = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 12, 0, 0) };
            AddFull(layout, statusLabel);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 0)
            };
            cancelButton = new Button { Text = "Abbrechen", AutoSize = true, MinimumSize = new Size(96, 0) };
            installButton = new Button { Text = isUpdate ? "Aktualisieren" : "Installieren", AutoSize = true, MinimumSize = new Size(96, 0) };
            cancelButton.Click += delegate { Close(); };
            installButton.Click += delegate { Install(); };
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(installButton);
            AddFull(layout, buttons);

            AcceptButton = installButton;
            CancelButton = cancelButton;
        }

        static void AddFull(TableLayoutPanel layout, Control c)
        {
            layout.Controls.Add(c);
            layout.SetColumnSpan(c, 2);
        }

        void Install()
        {
            installButton.Enabled = false;
            cancelButton.Enabled = false;
            UseWaitCursor = true;
            try
            {
                SetStatus("Beende laufende Instanz…");
                SystemIntegration.CloseRunningInstance(5000);

                SetStatus("Kopiere Dateien…");
                Directory.CreateDirectory(AppInfo.InstallDir);
                string target = AppInfo.InstalledExePath;
                WritePayload(target);

                SetStatus("Erstelle Verknüpfungen…");
                SystemIntegration.CreateShortcut(AppInfo.StartMenuShortcut, target, AppInfo.Description);
                if (desktopShortcut.Checked)
                    SystemIntegration.CreateShortcut(AppInfo.DesktopShortcut, target, AppInfo.Description);
                else
                    SystemIntegration.DeleteFileQuietly(AppInfo.DesktopShortcut);

                SystemIntegration.RemoveLegacyAutostart();
                SystemIntegration.SetAutostart(autostart.Checked, target);
                SystemIntegration.RegisterUninstallEntry(target, new FileInfo(target).Length);

                if (launchAfter.Checked)
                    Process.Start(new ProcessStartInfo(target) { WorkingDirectory = AppInfo.InstallDir });

                UseWaitCursor = false;
                MessageBox.Show(this,
                    AppInfo.Name + " wurde erfolgreich " + (isUpdate ? "aktualisiert" : "installiert") + ".\n\n" +
                    "Du findest es im Startmenü. Deinstallieren kannst du es über\n" +
                    "Einstellungen › Apps › Installierte Apps.",
                    AppInfo.Name + " Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                SetStatus("");
                MessageBox.Show(this, "Die Installation ist fehlgeschlagen:\n\n" + ex.Message,
                    AppInfo.Name + " Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                installButton.Enabled = true;
                cancelButton.Enabled = true;
            }
        }

        void SetStatus(string text)
        {
            statusLabel.Text = text;
            statusLabel.Refresh();
        }

        static void WritePayload(string target)
        {
            using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(SetupProgram.PayloadName))
            {
                if (payload == null) throw new InvalidOperationException("Installationsdaten fehlen im Setup.");

                // Die alte Datei kann kurz nach dem Beenden noch gesperrt sein -> ein paar Versuche.
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        using (var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                            payload.CopyTo(file);
                        return;
                    }
                    catch (IOException)
                    {
                        if (attempt >= 10) throw;
                        payload.Position = 0;
                        Thread.Sleep(500);
                    }
                }
            }
        }
    }
}
