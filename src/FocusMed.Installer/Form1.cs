using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FocusMed.Installer
{
    public partial class Form1 : Form
    {
        private Button btnInstall;
        private Label lblStatus;
        private ProgressBar progressBar;
        private PictureBox logoBox;

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "FocusMed Setup";
            this.Size = new Size(500, 350);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.White;

            logoBox = new PictureBox
            {
                Size = new Size(120, 120),
                Location = new Point(190, 20),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png");
                if (stream != null)
                {
                    logoBox.Image = Image.FromStream(stream);
                }
            }
            catch { }

            var titleLabel = new Label
            {
                Text = "Install FocusMed Clinical Platform",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(480, 30),
                Location = new Point(0, 150)
            };

            lblStatus = new Label
            {
                Text = "Ready to install to C:\\FocusMed",
                Font = new Font("Segoe UI", 9),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(480, 20),
                Location = new Point(0, 190)
            };

            progressBar = new ProgressBar
            {
                Size = new Size(400, 20),
                Location = new Point(42, 220),
                Style = ProgressBarStyle.Continuous
            };

            btnInstall = new Button
            {
                Text = "Install Now",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(150, 40),
                Location = new Point(167, 260),
                BackColor = Color.FromArgb(14, 165, 233), // Tailwind blue-500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += BtnInstall_Click;

            this.Controls.Add(logoBox);
            this.Controls.Add(titleLabel);
            this.Controls.Add(lblStatus);
            this.Controls.Add(progressBar);
            this.Controls.Add(btnInstall);
        }

        private async void BtnInstall_Click(object? sender, EventArgs e)
        {
            btnInstall.Enabled = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            
            await Task.Run(() => PerformInstallation());
            
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 100;
            lblStatus.Text = "Installation Complete!";
            btnInstall.Text = "Close";
            btnInstall.Click -= BtnInstall_Click;
            btnInstall.Click += (s, args) => Application.Exit();
            btnInstall.Enabled = true;
        }

        private void PerformInstallation()
        {
            try
            {
                string targetDir = @"C:\FocusMed";
                
                UpdateStatus("Stopping existing services...");
                RunCmd("sc", "stop FocusMed");
                Task.Delay(2000).Wait(); // Wait for stop
                
                UpdateStatus("Extracting files...");
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))
                {
                    if (stream == null) throw new Exception("Payload.zip not found in resources.");
                    
                    var tempZip = Path.Combine(Path.GetTempPath(), "Payload.zip");
                    using (var fs = new FileStream(tempZip, FileMode.Create))
                    {
                        stream.CopyTo(fs);
                    }
                    
                    // Overwrite extract
                    using (var archive = ZipFile.OpenRead(tempZip))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            var destPath = Path.Combine(targetDir, entry.FullName);
                            var dir = Path.GetDirectoryName(destPath);
                            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            if (!string.IsNullOrEmpty(entry.Name)) // not a directory
                            {
                                entry.ExtractToFile(destPath, true);
                            }
                        }
                    }
                    File.Delete(tempZip);
                }

                UpdateStatus("Registering Windows Service...");
                var workerExe = Path.Combine(targetDir, @"Worker\FocusMed.Worker.exe");
                
                // Unregister first if exists
                RunCmd("sc", "delete FocusMed");
                Task.Delay(1000).Wait();
                
                RunCmd("sc", $"create FocusMed binPath= \"{workerExe}\" start= auto");
                RunCmd("sc", "description FocusMed \"FocusMed Clinical Platform Background Worker\"");
                
                UpdateStatus("Starting Windows Service...");
                RunCmd("sc", "start FocusMed");

                UpdateStatus("Creating shortcuts...");
                var notifierExe = Path.Combine(targetDir, @"Notifier\FocusMed.Notifier.exe");
                
                // Create Desktop Shortcut
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcut(Path.Combine(desktopPath, "FocusMed Notifier.lnk"), notifierExe);

                // Create Startup Shortcut
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                CreateShortcut(Path.Combine(startupPath, "FocusMed Notifier.lnk"), notifierExe);

                UpdateStatus("Launching Notifier...");
                Process.Start(new ProcessStartInfo(notifierExe) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Installation failed.");
            }
        }

        private void UpdateStatus(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => lblStatus.Text = message));
            }
            else
            {
                lblStatus.Text = message;
            }
        }

        private void RunCmd(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            try
            {
                var p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch { }
        }

        private void CreateShortcut(string shortcutPath, string targetPath)
        {
            try
            {
                // Using WshShell via COM to create a shortcut without adding an external reference
                Type t = Type.GetTypeFromProgID("WScript.Shell")!;
                dynamic shell = Activator.CreateInstance(t)!;
                var shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();
            }
            catch { }
        }
    }
}
