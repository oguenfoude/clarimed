using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FocusMed.Notifier;

public class SplashForm : Form
{
    private Label statusLabel = null!;
    private ProgressBar progressBar = null!;
    private PictureBox logoBox = null!;
    private bool _launchSuccess = false;

    public bool LaunchSuccess => _launchSuccess;

    public SplashForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Size = new Size(400, 300);
        this.BackColor = Color.White;
        this.ShowInTaskbar = true;
        this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        logoBox = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(128, 128),
            Location = new Point((this.Width - 128) / 2, 40)
        };
        try { logoBox.Image = this.Icon?.ToBitmap(); } catch { }
        this.Controls.Add(logoBox);

        progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            Size = new Size(300, 10),
            Location = new Point(50, 200),
            MarqueeAnimationSpeed = 30
        };
        this.Controls.Add(progressBar);

        statusLabel = new Label
        {
            Text = "Starting FocusMed...",
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(300, 20),
            Location = new Point(50, 220),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
        };
        this.Controls.Add(statusLabel);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await StartBackendAsync();
    }

    private async Task StartBackendAsync()
    {
        try
        {
            statusLabel.Text = "Checking background services...";
            
            // Check if Worker is running (by exe name or dotnet hosting)
            var workers = Process.GetProcessesByName("FocusMed.Worker");
            if (workers.Length == 0)
            {
                // Also check for dotnet processes that might be hosting the Worker (dev mode: dotnet run)
                var dotnetProcs = Process.GetProcessesByName("dotnet");
                foreach (var dp in dotnetProcs)
                {
                    try
                    {
                        var cmdLine = dp.MainModule?.FileName ?? "";
                        if (cmdLine.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
                        {
                            workers = new[] { dp };
                            break;
                        }
                    }
                    catch { /* Access denied for some processes */ }
                }
            }
            {
                statusLabel.Text = "Starting Local Server...";
                
                // Try to find the worker exe
                var baseDir = AppContext.BaseDirectory;
                var productionWorker = Path.GetFullPath(Path.Combine(baseDir, "..", "Worker", "FocusMed.Worker.exe"));
                var devWorker = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "FocusMed.Worker", "bin", "Debug", "net10.0-windows", "win-x64", "FocusMed.Worker.exe"));
                var devWorker2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "FocusMed.Worker", "bin", "Debug", "net10.0-windows", "FocusMed.Worker.exe"));

                var workerPath = File.Exists(productionWorker) ? productionWorker
                               : File.Exists(devWorker) ? devWorker
                               : File.Exists(devWorker2) ? devWorker2
                               : null;

                if (workerPath != null)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = workerPath,
                        WorkingDirectory = Path.GetDirectoryName(workerPath),
                        CreateNoWindow = true,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
                else
                {
                    statusLabel.Text = "Warning: FocusMed.Worker.exe not found!";
                    await Task.Delay(2000);
                }
            }

            statusLabel.Text = "Starting Web Dashboard...";
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            bool isReady = false;
            
            // Poll for up to 30 seconds
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    var response = await client.GetAsync("http://localhost:5000/dashboard");
                    if (response.IsSuccessStatusCode)
                    {
                        isReady = true;
                        break;
                    }
                }
                catch { } // Ignore connection refused
                
                await Task.Delay(1000);
            }

            if (isReady)
            {
                statusLabel.Text = "Launching Browser...";
                await Task.Delay(500); // Let the user read it
                try { Process.Start(new ProcessStartInfo { FileName = "http://localhost:5000", UseShellExecute = true }); } catch { }
                _launchSuccess = true;
            }
            else
            {
                statusLabel.Text = "Error: Server failed to start.";
                await Task.Delay(3000);
            }
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Startup Error!";
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FocusMed", "logs");
            System.IO.Directory.CreateDirectory(logDir);
            System.IO.File.WriteAllText(Path.Combine(logDir, "crash_splash.log"), ex.ToString());
            await Task.Delay(3000);
        }
        finally
        {
            this.Close();
        }
    }
}
