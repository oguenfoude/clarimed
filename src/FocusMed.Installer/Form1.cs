using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

#pragma warning disable CS8618

namespace FocusMed.Installer
{
    public partial class Form1 : Form
    {
        // ── Shared UI ──
        private Panel pnlSidebar;
        private Panel pnlContent;
        private Label lblStepTitle;
        private Label lblStepDesc;
        private Label lblStatus;
        private ProgressBar progressBar;
        private PictureBox logoBox;
        private Button btnNext;
        private Button btnBack;

        // ── Step 1: Welcome ──

        // ── Step 2: Paths ──
        private TextBox txtInstallPath;
        private Button btnBrowseInstall;

        // ── Step 3: Options ──
        private CheckBox chkDesktopShortcut;
        private CheckBox chkStartupShortcut;

        // ── Step 4: Progress ──
        private ListBox lstLog;

        // ── Sidebar step labels ──
        private Label[] stepLabels;
        private Panel[] stepDots;

        private int _currentStep = 0;
        private bool _isUninstallMode;

        private readonly string[] _stepTitles = {
            "Welcome to FocusMed",
            "Choose Directory",
            "Options",
            "Installing..."
        };

        private readonly string[] _stepDescs = {
            "Setup will install or update FocusMed on your computer.",
            "Select where application and data files will be stored.",
            "Configure shortcuts and startup behavior.",
            "Please wait while FocusMed is being configured on your system."
        };

        public Form1(bool isUninstallMode = false)
        {
            _isUninstallMode = isUninstallMode;
            InitializeComponent();
            ShowStep(0);
        }

        private void InitializeComponent()
        {
            this.Text = "FocusMed Setup Wizard";
            this.Size = new Size(720, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9);

            // ═══════════════════════════════════════
            // LEFT SIDEBAR
            // ═══════════════════════════════════════
            pnlSidebar = new Panel
            {
                Size = new Size(200, 500),
                Location = new Point(0, 0),
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(15, 23, 42) // Slate-900
            };

            logoBox = new PictureBox
            {
                Size = new Size(64, 64),
                Location = new Point(68, 30),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png");
                if (stream != null) logoBox.Image = Image.FromStream(stream);
            }
            catch { }

            var lblBrand = new Label
            {
                Text = "FocusMed",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(200, 25),
                Location = new Point(0, 100),
                BackColor = Color.Transparent
            };

            var lblEdition = new Label
            {
                Text = "Clinical Platform",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(148, 163, 184), // Slate-400
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(200, 18),
                Location = new Point(0, 125),
                BackColor = Color.Transparent
            };

            // Sidebar step indicators
            stepLabels = new Label[4];
            stepDots = new Panel[4];
            string[] sideLabels = { "Welcome", "Directory", "Options", "Install" };
            for (int i = 0; i < 4; i++)
            {
                stepDots[i] = new Panel
                {
                    Size = new Size(8, 8),
                    Location = new Point(25, 175 + i * 40),
                    BackColor = Color.FromArgb(71, 85, 105) // Slate-600
                };
                // Round the dot
                var gp = new GraphicsPath();
                gp.AddEllipse(0, 0, 8, 8);
                stepDots[i].Region = new Region(gp);

                stepLabels[i] = new Label
                {
                    Text = sideLabels[i],
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.FromArgb(148, 163, 184),
                    Location = new Point(45, 170 + i * 40),
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
            }

            pnlSidebar.Controls.Add(logoBox);
            pnlSidebar.Controls.Add(lblBrand);
            pnlSidebar.Controls.Add(lblEdition);
            for (int i = 0; i < 4; i++)
            {
                pnlSidebar.Controls.Add(stepDots[i]);
                pnlSidebar.Controls.Add(stepLabels[i]);
            }

            // ═══════════════════════════════════════
            // RIGHT CONTENT AREA
            // ═══════════════════════════════════════
            pnlContent = new Panel
            {
                Location = new Point(200, 0),
                Size = new Size(520, 500),
                BackColor = Color.FromArgb(248, 250, 252)
            };

            lblStepTitle = new Label
            {
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(30, 25),
                AutoSize = true
            };

            lblStepDesc = new Label
            {
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(30, 55),
                Size = new Size(460, 20)
            };

            // ── Step 1: Welcome ──
            var lblWelcomeMsg = new Label
            {
                Text = "Click Next to continue with the installation or update.",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(30, 120),
                AutoSize = true
            };

            // ── Step 2: Paths ──
            var lblInstallPath = new Label
            {
                Text = "Installation Directory:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = new Point(30, 110),
                AutoSize = true
            };
            var lblInstallHint = new Label
            {
                Text = "Application files and database will be stored here.",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(30, 128),
                AutoSize = true
            };
            txtInstallPath = new TextBox
            {
                Text = @"C:\FocusMed",
                Location = new Point(30, 150),
                Size = new Size(370, 28),
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            btnBrowseInstall = new Button
            {
                Text = "Browse",
                Location = new Point(410, 149),
                Size = new Size(70, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };
            btnBrowseInstall.FlatAppearance.BorderSize = 0;
            btnBrowseInstall.Click += (s, e) => BrowseFolder(txtInstallPath);

            // Validation warning panel
            var pnlWarning = new Panel
            {
                Size = new Size(450, 40),
                Location = new Point(30, 200),
                BackColor = Color.FromArgb(254, 243, 199)
            };
            var lblWarning = new Label
            {
                Text = "⚠  Directories will be created automatically if they don't exist.",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(146, 64, 14),
                Location = new Point(10, 12),
                AutoSize = true
            };
            pnlWarning.Controls.Add(lblWarning);

            // ── Step 3: Options ──
            chkDesktopShortcut = new CheckBox
            {
                Text = "  Create Desktop Shortcut",
                Font = new Font("Segoe UI", 10),
                Location = new Point(50, 110),
                AutoSize = true,
                Checked = true
            };

            chkStartupShortcut = new CheckBox
            {
                Text = "  Auto-start FocusMed Notifier when Windows boots",
                Font = new Font("Segoe UI", 10),
                Location = new Point(50, 150),
                AutoSize = true,
                Checked = true
            };

            var lblOptionsHint = new Label
            {
                Text = "The Notifier tray app alerts you when new DICOM studies arrive\nand need patient assignment.",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(75, 180),
                Size = new Size(400, 35)
            };

            // ── Step 4: Progress ──
            lstLog = new ListBox
            {
                Location = new Point(30, 90),
                Size = new Size(450, 260),
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(148, 226, 213)
            };

            progressBar = new ProgressBar
            {
                Location = new Point(30, 360),
                Size = new Size(450, 12),
                Style = ProgressBarStyle.Marquee
            };

            lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(14, 165, 233),
                Location = new Point(30, 378),
                Size = new Size(450, 20)
            };

            // ── Navigation Buttons ──
            btnBack = new Button
            {
                Text = "← Back",
                Font = new Font("Segoe UI", 10),
                Size = new Size(100, 38),
                Location = new Point(280, 415),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click += (s, e) => ShowStep(_currentStep - 1);

            btnNext = new Button
            {
                Text = "Next →",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(120, 38),
                Location = new Point(390, 415),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White
            };
            btnNext.FlatAppearance.BorderSize = 0;
            btnNext.Click += BtnNext_Click;

            // Add ALL controls to content panel (visibility managed by ShowStep)
            pnlContent.Controls.Add(lblStepTitle);
            pnlContent.Controls.Add(lblStepDesc);
            // Step 1
            pnlContent.Controls.Add(lblWelcomeMsg);
            // Step 2
            pnlContent.Controls.Add(lblInstallPath);
            pnlContent.Controls.Add(lblInstallHint);
            pnlContent.Controls.Add(txtInstallPath);
            pnlContent.Controls.Add(btnBrowseInstall);
            pnlContent.Controls.Add(pnlWarning);
            // Step 3
            pnlContent.Controls.Add(chkDesktopShortcut);
            pnlContent.Controls.Add(chkStartupShortcut);
            pnlContent.Controls.Add(lblOptionsHint);
            // Step 4
            pnlContent.Controls.Add(lstLog);
            pnlContent.Controls.Add(progressBar);
            pnlContent.Controls.Add(lblStatus);
            // Nav
            pnlContent.Controls.Add(btnBack);
            pnlContent.Controls.Add(btnNext);

            this.Controls.Add(pnlSidebar);
            this.Controls.Add(pnlContent);

            // Tag controls by step for visibility toggling
            lblWelcomeMsg.Tag = 0;
            lblInstallPath.Tag = 1; lblInstallHint.Tag = 1; txtInstallPath.Tag = 1;
            btnBrowseInstall.Tag = 1; pnlWarning.Tag = 1;
            chkDesktopShortcut.Tag = 2; chkStartupShortcut.Tag = 2; lblOptionsHint.Tag = 2;
            lstLog.Tag = 3; progressBar.Tag = 3; lblStatus.Tag = 3;
        }

        // ═══════════════════════════════════════
        // STEP NAVIGATION
        // ═══════════════════════════════════════
        private void ShowStep(int step)
        {
            _currentStep = step;
            lblStepTitle.Text = _stepTitles[step];
            lblStepDesc.Text = _stepDescs[step];

            // Toggle visibility of tagged controls
            foreach (Control c in pnlContent.Controls)
            {
                if (c.Tag is int tagStep)
                    c.Visible = tagStep == step;
            }

            // Always show title, desc, and nav
            lblStepTitle.Visible = true;
            lblStepDesc.Visible = true;
            btnBack.Visible = step > 0 && step < 3;
            btnNext.Visible = step < 3;

            // Update sidebar highlights
            for (int i = 0; i < 4; i++)
            {
                bool active = i == step;
                bool done = i < step;
                stepDots[i].BackColor = active ? Color.FromArgb(14, 165, 233) :
                                         done ? Color.FromArgb(34, 197, 94) :
                                                Color.FromArgb(71, 85, 105);
                stepLabels[i].ForeColor = active ? Color.White :
                                           done ? Color.FromArgb(134, 239, 172) :
                                                  Color.FromArgb(148, 163, 184);
                stepLabels[i].Font = active ? new Font("Segoe UI", 9, FontStyle.Bold)
                                            : new Font("Segoe UI", 9);
            }

            // Step-specific adjustments
            if (step == 0)
            {
                btnNext.Text = "Next →";
            }
            else if (step == 2)
            {
                if (_isUninstallMode)
                {
                    // Skip options for uninstall, go straight to action
                    _currentStep = 3;
                    ShowStep(3);
                    return;
                }
                btnNext.Text = "Install →";
                btnNext.BackColor = Color.FromArgb(34, 197, 94); // Green
            }
            else
            {
                btnNext.Text = "Next →";
                btnNext.BackColor = Color.FromArgb(14, 165, 233); // Blue
            }
        }

        private async void BtnNext_Click(object? sender, EventArgs e)
        {
            if (_currentStep == 0)
            {
                if (_isUninstallMode)
                {
                    // For uninstall, skip straight to uninstallation
                    _stepTitles[3] = "Uninstalling...";
                    _stepDescs[3] = "Removing FocusMed from your system. Patient data will be preserved.";
                    
                    // Auto-detect install path from where the uninstaller is running
                    txtInstallPath.Text = Path.GetDirectoryName(Application.ExecutablePath) ?? @"C:\FocusMed";
                    
                    ShowStep(3);
                    BtnNext_Click(null, EventArgs.Empty);
                    return;
                }
                else
                {
                    _stepTitles[3] = "Installing...";
                    _stepDescs[3] = "Please wait while FocusMed is being configured on your system.";
                    ShowStep(1);
                }
            }
            else if (_currentStep == 1)
            {
                if (string.IsNullOrWhiteSpace(txtInstallPath.Text))
                {
                    MessageBox.Show("Please provide an installation directory path.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ShowStep(2);
            }
            else if (_currentStep == 2)
            {
                ShowStep(3);
            }

            // Step 3 = auto-execute
            if (_currentStep == 3)
            {
                btnBack.Visible = false;
                btnNext.Visible = false;
                lstLog.Items.Clear();

                string installPath = txtInstallPath.Text;
                string dataPath = Path.Combine(installPath, "data");
                bool createDesktop = chkDesktopShortcut.Checked;
                bool createStartup = chkStartupShortcut.Checked;

                bool success;
                if (_isUninstallMode)
                    success = await Task.Run(() => PerformUninstallation(installPath, dataPath));
                else
                    success = await Task.Run(() => PerformInstallation(installPath, dataPath, createDesktop, createStartup));

                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 100;

                if (success)
                {
                    string msg = _isUninstallMode ? "Uninstallation Complete!" : "Installation Complete!";
                    UpdateStatus(msg);
                    LogStep("✓ " + msg);

                    // Update sidebar
                    stepDots[3].BackColor = Color.FromArgb(34, 197, 94);
                    stepLabels[3].ForeColor = Color.FromArgb(134, 239, 172);
                    stepLabels[3].Text = "Done ✓";

                    btnNext.Visible = true;
                    btnNext.Text = "Close";
                    btnNext.BackColor = Color.FromArgb(34, 197, 94);
                    btnNext.Click -= BtnNext_Click;
                    btnNext.Click += (s, args) => Application.Exit();
                }
                else
                {
                    btnNext.Visible = true;
                    btnNext.Text = "Close";
                    btnNext.BackColor = Color.FromArgb(239, 68, 68);
                    btnNext.Click -= BtnNext_Click;
                    btnNext.Click += (s, args) => Application.Exit();
                }
            }
        }

        private void BrowseFolder(TextBox targetTextBox)
        {
            using var fbd = new FolderBrowserDialog();
            fbd.SelectedPath = targetTextBox.Text;
            if (fbd.ShowDialog() == DialogResult.OK)
                targetTextBox.Text = fbd.SelectedPath;
        }

        // ═══════════════════════════════════════
        // INSTALLATION LOGIC
        // ═══════════════════════════════════════
        private bool PerformInstallation(string targetDir, string dataDir, bool createDesktop, bool createStartup)
        {
            try
            {
                LogStep("Creating directories...");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                LogStep("Stopping existing services...");
                RunCmd("sc", "stop FocusMed");
                var procs = Process.GetProcessesByName("FocusMed");
                foreach (var proc in procs)
                {
                    try { proc.Kill(); } catch { }
                }
                Task.Delay(2000).Wait();

                LogStep("Extracting application files...");
                UpdateStatus("Extracting...");
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
                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                entry.ExtractToFile(destPath, true);
                            }
                        }
                    }
                    File.Delete(tempZip);
                }
                LogStep("✓ Files extracted");

                // ── Copy templates to data directory so user can edit them ──
                string sourceTemplates = Path.Combine(targetDir, @"Worker\templates");
                string destTemplates = Path.Combine(dataDir, "templates");
                if (!Directory.Exists(destTemplates)) Directory.CreateDirectory(destTemplates);
                if (Directory.Exists(sourceTemplates))
                {
                    foreach (var file in Directory.GetFiles(sourceTemplates))
                    {
                        var destFile = Path.Combine(destTemplates, Path.GetFileName(file));
                        if (!File.Exists(destFile))
                        {
                            File.Copy(file, destFile);
                        }
                    }
                }

                LogStep("Configuring application paths...");
                UpdateStatus("Configuring...");
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

                        focusMedNode["DatabasePath"] = Path.Combine(dataDir, @"db\focusmed.db");
                        focusMedNode["ArchivePath"] = Path.Combine(dataDir, @"archive");
                        focusMedNode["ImagesPath"] = Path.Combine(dataDir, @"images");
                        focusMedNode["WatchFolderPath"] = Path.Combine(dataDir, @"WatchFolder");
                        focusMedNode["DocumentOutputPath"] = Path.Combine(dataDir, @"Documents");
                        focusMedNode["MergedPdfOutputPath"] = Path.Combine(dataDir, @"Output");
                        focusMedNode["CoverPageTemplatePath"] = Path.Combine(dataDir, @"templates\pagegarde.docx");

                        File.WriteAllText(appSettingsPath, jsonNode.ToString());
                    }
                }
                LogStep("✓ Paths configured");

                LogStep("Registering Windows Service...");
                UpdateStatus("Service registration...");
                var workerExe = Path.Combine(targetDir, @"Worker\FocusMed.Worker.exe");
                RunCmd("sc", "delete FocusMed");
                Task.Delay(1000).Wait();
                RunCmd("sc", $"create FocusMed binPath= \"{workerExe}\" start= auto");
                RunCmd("sc", "description FocusMed \"FocusMed Clinical Platform Background Worker\"");
                LogStep("✓ Windows Service registered");

                LogStep("Configuring Virtual Printer...");
                UpdateStatus("Printer setup...");
                string portName = Path.Combine(dataDir, @"WatchFolder\incoming_print.pdf");
                string psCmd = $"Remove-Printer -Name 'FocusMed' -ErrorAction SilentlyContinue; " +
                               $"Add-PrinterPort -Name '{portName}' -ErrorAction SilentlyContinue; " +
                               $"Add-Printer -Name 'FocusMed' -DriverName 'Microsoft Print To PDF' -PortName '{portName}'";
                RunCmd("powershell", $"-NoProfile -Command \"{psCmd}\"");
                LogStep("✓ Virtual Printer configured");

                LogStep("Starting Windows Service...");
                UpdateStatus("Starting service...");
                RunCmd("sc", "start FocusMed");
                LogStep("✓ Service started");

                var notifierExe = Path.Combine(targetDir, @"Notifier\FocusMed.exe");

                if (createDesktop)
                {
                    LogStep("Creating Desktop shortcut...");
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktopPath, "FocusMed.lnk"), notifierExe);
                }

                if (createStartup)
                {
                    LogStep("Creating Startup shortcut...");
                    string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    CreateShortcut(Path.Combine(startupPath, "FocusMed.lnk"), notifierExe);
                }

                LogStep("Deploying management tools...");
                UpdateStatus("Deploying tools...");
                
                string uninstallExe = Path.Combine(targetDir, "FocusMed-Uninstall.exe");
                string restartExe = Path.Combine(targetDir, "FocusMed-Restart.exe");
                
                if (Application.ExecutablePath != uninstallExe)
                    File.Copy(Application.ExecutablePath, uninstallExe, true);
                    
                if (Application.ExecutablePath != restartExe)
                    File.Copy(Application.ExecutablePath, restartExe, true);
                    
                LogStep("✓ Management tools deployed");

                LogStep("Launching Notifier...");
                Process.Start(new ProcessStartInfo(notifierExe) { UseShellExecute = true });
                LogStep("✓ Notifier launched");

                return true;
            }
            catch (Exception ex)
            {
                LogStep("✗ ERROR: " + ex.Message);
                UpdateStatus("Installation failed.");
                return false;
            }
        }

        // ═══════════════════════════════════════
        // UNINSTALLATION LOGIC
        // ═══════════════════════════════════════
        private bool PerformUninstallation(string targetDir, string dataDir)
        {
            try
            {
                LogStep("Stopping services and applications...");
                UpdateStatus("Stopping services...");
                RunCmd("sc", "stop FocusMed");
                var procs = Process.GetProcessesByName("FocusMed");
                foreach (var proc in procs)
                {
                    try { proc.Kill(); } catch { }
                }
                Task.Delay(2000).Wait();

                LogStep("Removing Windows Service...");
                UpdateStatus("Removing service...");
                RunCmd("sc", "delete FocusMed");
                Task.Delay(1000).Wait();
                LogStep("✓ Service removed");

                LogStep("Removing Virtual Printer...");
                string psCmd = "Remove-Printer -Name 'FocusMed' -ErrorAction SilentlyContinue";
                RunCmd("powershell", $"-NoProfile -Command \"{psCmd}\"");
                LogStep("✓ Printer removed");

                LogStep("Removing shortcuts...");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string deskLink = Path.Combine(desktopPath, "FocusMed.lnk");
                if (File.Exists(deskLink)) File.Delete(deskLink);

                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string startLink = Path.Combine(startupPath, "FocusMed.lnk");
                if (File.Exists(startLink)) File.Delete(startLink);
                LogStep("✓ Shortcuts removed");

                LogStep("Deleting application binaries...");
                UpdateStatus("Cleaning up...");
                if (Directory.Exists(targetDir))
                {
                    foreach (var d in Directory.GetDirectories(targetDir))
                    {
                        if (!d.EndsWith("data", StringComparison.OrdinalIgnoreCase))
                        {
                            try { Directory.Delete(d, true); } catch { }
                        }
                    }
                    foreach (var f in Directory.GetFiles(targetDir))
                    {
                        if (f != Application.ExecutablePath)
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }
                }
                LogStep("✓ Application files deleted");

                LogStep("Patient data preserved at: " + dataDir);

                // Schedule self-deletion of the uninstaller executable
                string batPath = Path.Combine(Path.GetTempPath(), "focusmed_cleanup.bat");
                File.WriteAllText(batPath, $"@echo off\r\nping 127.0.0.1 -n 3 > nul\r\ndel /f /q \"{Application.ExecutablePath}\"\r\ndel \"%~f0\"");
                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                LogStep("Patient data preserved at: " + dataDir);

                return true;
            }
            catch (Exception ex)
            {
                LogStep("✗ ERROR: " + ex.Message);
                UpdateStatus("Uninstallation failed.");
                return false;
            }
        }

        // ═══════════════════════════════════════
        // STATIC RESTART
        // ═══════════════════════════════════════
        public static void RestartSystem()
        {
            try
            {
                RunCmd("sc", "stop FocusMed");
                var procs = Process.GetProcessesByName("FocusMed");
                foreach (var proc in procs)
                {
                    try { proc.Kill(); } catch { }
                }
                Task.Delay(2000).Wait();
                RunCmd("sc", "start FocusMed");

                string exePath = Application.ExecutablePath;
                string dir = Path.GetDirectoryName(exePath) ?? "";
                string notifierExe = Path.Combine(dir, @"Notifier\FocusMed.exe");
                if (File.Exists(notifierExe))
                {
                    Process.Start(new ProcessStartInfo(notifierExe) { UseShellExecute = true });
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════
        private void LogStep(string message)
        {
            if (lstLog.InvokeRequired)
            {
                lstLog.Invoke(new Action(() =>
                {
                    lstLog.Items.Add(message);
                    lstLog.TopIndex = lstLog.Items.Count - 1;
                }));
            }
            else
            {
                lstLog.Items.Add(message);
                lstLog.TopIndex = lstLog.Items.Count - 1;
            }
        }

        private void UpdateStatus(string message)
        {
            if (lblStatus.InvokeRequired)
                lblStatus.Invoke(new Action(() => lblStatus.Text = message));
            else
                lblStatus.Text = message;
        }

        private static void RunCmd(string exe, string args)
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
