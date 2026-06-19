using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ClariMed.Data;

namespace ClariMed.Notifier;

public class NotifierApplicationContext : ApplicationContext
{
    private NotifyIcon _notifyIcon;
    private ContextMenuStrip _contextMenu;
    private System.Windows.Forms.Timer _timer;
    private bool _hasUpdate = false;
    private string _latestVersion = "";
    private ToolStripMenuItem _updateMenuItem;

    public NotifierApplicationContext()
    {
        _contextMenu = new ContextMenuStrip();

        var openMenuItem = new ToolStripMenuItem("Open Dashboard", null, OpenDashboard);
        _updateMenuItem = new ToolStripMenuItem("Apply Update", null, ApplyUpdate) { Visible = false };
        var exitMenuItem = new ToolStripMenuItem("Exit", null, Exit);

        _contextMenu.Items.Add(openMenuItem);
        _contextMenu.Items.Add(_updateMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitMenuItem);

        _notifyIcon = new NotifyIcon()
        {
            Icon = SystemIcons.Application,
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = "ClariMed Server"
        };
        _notifyIcon.DoubleClick += OpenDashboard;

        _timer = new System.Windows.Forms.Timer { Interval = 60000 };
        _timer.Tick += CheckForUpdates;
        _timer.Start();

        CheckForUpdates(null, EventArgs.Empty);
    }

    private void OpenDashboard(object? sender, EventArgs e)
    {
        // PowerShell script to find an Edge/Chrome window with "ClariMed" in title and switch to it,
        // or just start the URL if not possible.
        // A simpler way to reuse an existing tab is to just launch the URL and let the browser handle it,
        // but standard Process.Start always opens a new tab. 
        // Using PowerShell UIAutomation to find and focus is robust.
        var script = @"
$url = 'http://localhost:5000/dashboard'
$title = 'ClariMed'
# Try to find a process with the title
$proc = Get-Process | Where-Object { $_.MainWindowTitle -match $title } | Select-Object -First 1
if ($proc) {
    # Activate window via interop
    Add-Type @'
        using System;
        using System.Runtime.InteropServices;
        public class Win32 {
            [DllImport(""user32.dll"")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }
'@
    [Win32]::SetForegroundWindow($proc.MainWindowHandle)
} else {
    Start-Process $url
}";
        var psi = new ProcessStartInfo("powershell", $"-NoProfile -WindowStyle Hidden -Command \"{script.Replace("\"", "\\\"")}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }

    private void ApplyUpdate(object? sender, EventArgs e)
    {
        // 3d) When "Apply Update" is clicked, run a simple update.bat and exit the tray app.
        var batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.bat");
        File.WriteAllText(batPath, "@echo off\r\necho Updating ClariMed...\r\npause");
        Process.Start(new ProcessStartInfo(batPath) { UseShellExecute = true });
        Exit(sender, e);
    }

    private async void CheckForUpdates(object? sender, EventArgs e)
    {
        try
        {
            var dbPath = @"D:\ClariMed\db\clarimed.db";
            if (!File.Exists(dbPath)) return; // Or whatever path

            var optionsBuilder = new DbContextOptionsBuilder<ClariMedDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            using var db = new ClariMedDbContext(optionsBuilder.Options);
            var settings = await db.ClinicSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
            if (settings != null)
            {
                if (settings.UpdateAvailable && !_hasUpdate)
                {
                    _hasUpdate = true;
                    _latestVersion = settings.LatestVersion;
                    _updateMenuItem.Text = $"Apply Update ({_latestVersion})";
                    _updateMenuItem.Visible = true;
                    _notifyIcon.ShowBalloonTip(5000, "ClariMed Update", $"Update {_latestVersion} is available! Click to update.", ToolTipIcon.Info);
                }
                else if (!settings.UpdateAvailable && _hasUpdate)
                {
                    _hasUpdate = false;
                    _updateMenuItem.Visible = false;
                }
            }
        }
        catch { }
    }

    private void Exit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _timer.Stop();
        Application.Exit();
    }
}
