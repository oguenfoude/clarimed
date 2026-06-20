using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json.Nodes;
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
        private TextBox txtInstallPath;
        private TextBox txtDataPath;
        private Button btnBrowseInstall;
        private Button btnBrowseData;

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "FocusMed Setup";
            this.Size = new Size(550, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.White;

            logoBox = new PictureBox
            {
                Size = new Size(120, 120),
                Location = new Point(215, 20),
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
                Size = new Size(530, 30),
                Location = new Point(0, 150)
            };

            // Install Path UI
            var lblInstallPath = new Label
            {
                Text = "Application Files Directory:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(40, 190),
                AutoSize = true
            };
            
            txtInstallPath = new TextBox
            {
                Text = @"C:\Program Files\FocusMed",
                Location = new Point(40, 210),
                Size = new Size(380, 23)
            };

            btnBrowseInstall = new Button
            {
                Text = "Browse...",
                Location = new Point(430, 209),
                Size = new Size(75, 25)
            };
            btnBrowseInstall.Click += (s, e) => BrowseFolder(txtInstallPath);

            // Data Path UI
            var lblDataPath = new Label
            {
                Text = "Database & DICOM Data Directory:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(40, 245),
                AutoSize = true
            };
            
            txtDataPath = new TextBox
            {
                Text = @"C:\FocusMedData",
                Location = new Point(40, 265),
                Size = new Size(380, 23)
            };

            btnBrowseData = new Button
            {
                Text = "Browse...",
                Location = new Point(430, 264),
                Size = new Size(75, 25)
            };
            btnBrowseData.Click += (s, e) => BrowseFolder(txtDataPath);

            lblStatus = new Label
            {
                Text = "Ready to install",
                Font = new Font("Segoe UI", 9),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(530, 20),
                Location = new Point(0, 310)
            };

            progressBar = new ProgressBar
            {
                Size = new Size(465, 20),
                Location = new Point(40, 335),
                Style = ProgressBarStyle.Continuous
            };

            btnInstall = new Button
            {
                Text = "Install Now",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(150, 40),
                Location = new Point(192, 380),
                BackColor = Color.FromArgb(14, 165, 233), // Tailwind blue-500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += BtnInstall_Click;

            this.Controls.Add(logoBox);
            this.Controls.Add(titleLabel);
            this.Controls.Add(lblInstallPath);
            this.Controls.Add(txtInstallPath);
            this.Controls.Add(btnBrowseInstall);
            this.Controls.Add(lblDataPath);
            this.Controls.Add(txtDataPath);
            this.Controls.Add(btnBrowseData);
            this.Controls.Add(lblStatus);
            this.Controls.Add(progressBar);
            this.Controls.Add(btnInstall);
        }

        private void BrowseFolder(TextBox targetTextBox)
        {
            using var fbd = new FolderBrowserDialog();
            fbd.SelectedPath = targetTextBox.Text;
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                targetTextBox.Text = fbd.SelectedPath;
            }
        }

        private async void BtnInstall_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInstallPath.Text) || string.IsNullOrWhiteSpace(txtDataPath.Text))
            {
                MessageBox.Show("Please provide both installation and data paths.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnInstall.Enabled = false;
            txtInstallPath.Enabled = false;
            txtDataPath.Enabled = false;
            btnBrowseInstall.Enabled = false;
            btnBrowseData.Enabled = false;
            
            progressBar.Style = ProgressBarStyle.Marquee;
            
            string installPath = txtInstallPath.Text;
            string dataPath = txtDataPath.Text;

            bool success = await Task.Run(() => PerformInstallation(installPath, dataPath));
            
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 100;

            if (success)
            {
                lblStatus.Text = "Installation Complete!";
                btnInstall.Text = "Close";
                btnInstall.Click -= BtnInstall_Click;
                btnInstall.Click += (s, args) => Application.Exit();
            }
            
            btnInstall.Enabled = true;
        }

        private bool PerformInstallation(string targetDir, string dataDir)
        {
            try
            {
                UpdateStatus("Stopping existing services...");
                RunCmd("sc", "stop FocusMed");
                Task.Delay(2000).Wait(); // Wait for stop
                
                UpdateStatus("Extracting application files...");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))
                {
                    if (stream == null) throw new Exception("Payload.zip not found in resources.");
                    
                    var tempZip = Path.Combine(Path.GetTempPath(), "Payload.zip");
                    using (var fs = new FileStream(tempZip, FileMode.Create))
                    {
                        stream.CopyTo(fs);
                    }
                    
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

                UpdateStatus("Configuring application paths...");
                string appSettingsPath = Path.Combine(targetDir, @"Worker\appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    var jsonStr = File.ReadAllText(appSettingsPath);
                    var jsonNode = JsonNode.Parse(jsonStr);
                    if (jsonNode != null)
                    {
                        var focusMedNode = jsonNode["FocusMed"];
                        if (focusMedNode == null)
                        {
                            focusMedNode = new JsonObject();
                            jsonNode.AsObject().Add("FocusMed", focusMedNode);
                        }

                        // Inject the user's chosen absolute paths
                        focusMedNode["DatabasePath"] = Path.Combine(dataDir, @"db\focusmed.db");
                        focusMedNode["ArchivePath"] = Path.Combine(dataDir, @"archive");
                        focusMedNode["ImagesPath"] = Path.Combine(dataDir, @"images");
                        focusMedNode["WatchFolderPath"] = Path.Combine(dataDir, @"WatchFolder");
                        focusMedNode["DocumentOutputPath"] = Path.Combine(dataDir, @"Documents");

                        File.WriteAllText(appSettingsPath, jsonNode.ToString());
                    }
                }

                UpdateStatus("Registering Windows Service...");
                var workerExe = Path.Combine(targetDir, @"Worker\FocusMed.Worker.exe");
                
                // Unregister first if exists
                RunCmd("sc", "delete FocusMed");
                Task.Delay(1000).Wait();
                
                RunCmd("sc", $"create FocusMed binPath= \"{workerExe}\" start= auto");
                RunCmd("sc", "description FocusMed \"FocusMed Clinical Platform Background Worker\"");
                
                UpdateStatus("Configuring Virtual Printer...");
                string portName = Path.Combine(dataDir, @"WatchFolder\incoming_print.pdf");
                string psCmd = $"Remove-Printer -Name 'FocusMed' -ErrorAction SilentlyContinue; " +
                               $"Add-PrinterPort -Name '{portName}' -ErrorAction SilentlyContinue; " +
                               $"Add-Printer -Name 'FocusMed' -DriverName 'Microsoft Print To PDF' -PortName '{portName}'";
                RunCmd("powershell", $"-NoProfile -Command \"{psCmd}\"");
                
                UpdateStatus("Starting Windows Service...");
                RunCmd("sc", "start FocusMed");

                UpdateStatus("Creating shortcuts...");
                var notifierExe = Path.Combine(targetDir, @"Notifier\FocusMed.Notifier.exe");
                
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcut(Path.Combine(desktopPath, "FocusMed Notifier.lnk"), notifierExe);

                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                CreateShortcut(Path.Combine(startupPath, "FocusMed Notifier.lnk"), notifierExe);

                UpdateStatus("Launching Notifier...");
                Process.Start(new ProcessStartInfo(notifierExe) { UseShellExecute = true });
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus("Installation failed.");
                return false;
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
