using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

#pragma warning disable CS0414

namespace FocusMed.Notifier;

public class NotifierApplicationContext : ApplicationContext
{
    private NotifyIcon _notifyIcon;
    private ContextMenuStrip _contextMenu;
    private System.Windows.Forms.Timer _timer;
    private bool _hasUpdate = false;
    private string _latestVersion = "";
    private ToolStripMenuItem _updateMenuItem;
    private readonly HttpClient _http;
    private QuickAssignWindow? _currentAssignWindow;
    private HashSet<int> _shownDocumentIds = new();
    private readonly string _logDir;
    private readonly string _logFile;

    public NotifierApplicationContext()
    {
        _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FocusMed", "logs");
        Directory.CreateDirectory(_logDir);
        _logFile = Path.Combine(_logDir, "notifier.log");

        Log("Notifier started");

        _http = new HttpClient { BaseAddress = new Uri("http://localhost:5000"), Timeout = TimeSpan.FromSeconds(10) };
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
            Text = "FocusMed - Starting..."
        };
        _notifyIcon.DoubleClick += OpenDashboard;

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += async (s, e) => await PollPendingDocuments();
        _timer.Start();

        Log("Timer started (2s interval), polling for pending documents");
    }

    private async Task PollPendingDocuments()
    {
        try
        {
            var pendingDocs = await _http.GetFromJsonAsync<JsonElement[]>("/api/quickassign/pending");
            int count = pendingDocs?.Length ?? 0;

            _notifyIcon.Text = count > 0
                ? $"FocusMed - {count} document(s) to assign"
                : "FocusMed - No pending documents";

            if (pendingDocs != null && pendingDocs.Length > 0)
            {
                Log($"Found {pendingDocs.Length} pending document(s), shownDocumentIds={_shownDocumentIds.Count}, currentWindow={_currentAssignWindow != null}");

                foreach (var nextDoc in pendingDocs)
                {
                    var docId = nextDoc.GetProperty("id").GetInt32();

                    if (!_shownDocumentIds.Contains(docId) && _currentAssignWindow == null)
                    {
                        _shownDocumentIds.Add(docId);

                        Log($"Opening QuickAssignWindow for docId={docId}");
                        _currentAssignWindow = new QuickAssignWindow(docId);
                        _currentAssignWindow.FormClosed += (s, e) =>
                        {
                            _currentAssignWindow = null;
                            Log("QuickAssignWindow closed");
                        };
                        _currentAssignWindow.Show();
                        _currentAssignWindow.Activate();
                        break;
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Log($"HTTP error polling pending docs: {ex.StatusCode} - {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Log("Poll request timed out");
        }
        catch (Exception ex)
        {
            Log($"Unexpected error polling: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(_logFile, line);
        }
        catch { /* Logging should never crash the app */ }
    }

    private void OpenDashboard(object? sender, EventArgs e)
    {
        var script = @"
$url = 'http://localhost:5000/dashboard'
$title = 'FocusMed'
$proc = Get-Process | Where-Object { $_.MainWindowTitle -match $title } | Select-Object -First 1
if ($proc) {
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
        var batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.bat");
        File.WriteAllText(batPath, "@echo off\r\necho Updating FocusMed...\r\npause");
        Process.Start(new ProcessStartInfo(batPath) { UseShellExecute = true });
        Exit(sender, e);
    }

    private void Exit(object? sender, EventArgs e)
    {
        Log("Notifier exiting");
        _notifyIcon.Visible = false;
        _timer.Stop();
        Application.Exit();
    }
}
