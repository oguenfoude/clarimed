using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

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

    public NotifierApplicationContext()
    {
        _http = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
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
            Text = "FocusMed Server"
        };
        _notifyIcon.DoubleClick += OpenDashboard;

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += async (s, e) => await PollPendingDocuments();
        _timer.Start();
    }

    private async Task PollPendingDocuments()
    {
        try
        {
            var pendingDocs = await _http.GetFromJsonAsync<JsonElement[]>("/api/quickassign/pending");
            if (pendingDocs != null && pendingDocs.Length > 0)
            {
                foreach (var nextDoc in pendingDocs)
                {
                    var docId = nextDoc.GetProperty("id").GetInt32();

                    // If we haven't shown a window for this document, and no window is currently open
                    if (!_shownDocumentIds.Contains(docId) && _currentAssignWindow == null)
                    {
                        _shownDocumentIds.Add(docId);
                        
                        _currentAssignWindow = new QuickAssignWindow(docId);
                        _currentAssignWindow.FormClosed += (s, e) => _currentAssignWindow = null;
                        _currentAssignWindow.Show();
                        _currentAssignWindow.Activate();
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(@"D:\ClariMed\notifier_error.log", $"Poll Error: {ex}\n");
        }
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
        _notifyIcon.Visible = false;
        _timer.Stop();
        Application.Exit();
    }
}
