using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ClariMed.VirtualPrinter;
using ClariMed.Data.Repositories;
using ClariMed.Data.Services;
using ClariMed.Data.Models;

namespace ClariMed.Worker.Services;

public class VirtualPrinterService : BackgroundService
{
    private readonly ILogger<VirtualPrinterService> _logger;
    private readonly WindowsPrinterRegistration _registration;
    private readonly IServiceScopeFactory _scopeFactory;
    private FileSystemWatcher? _watcher;
    private string? _watchFile;
    private readonly SemaphoreSlim _processLock = new SemaphoreSlim(1, 1);

    public VirtualPrinterService(
        ILogger<VirtualPrinterService> logger, 
        WindowsPrinterRegistration registration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _registration = registration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Virtual Printer Service...");
        await _registration.EnsureRegisteredAsync();

        using (var scope = _scopeFactory.CreateScope())
        {
            var settingsRepo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
            var settings = await settingsRepo.GetAsync();
            var watchDir = settings.WatchFolderPath;
            Directory.CreateDirectory(watchDir);
            _watchFile = Path.Combine(watchDir, "VirtualPrint.pdf");

            // Clean up any stale file from previous sessions
            if (File.Exists(_watchFile))
            {
                try { File.Delete(_watchFile); } catch { }
            }

            _watcher = new FileSystemWatcher(watchDir, "VirtualPrint.pdf")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            
            // Wire both Created and Changed events to catch spooler actions
            _watcher.Created += (s, e) => ProcessPrintJobAsync(stoppingToken);
            _watcher.Changed += (s, e) => ProcessPrintJobAsync(stoppingToken);
        }

        // Just keep the background service alive
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void ProcessPrintJobAsync(CancellationToken ct)
    {
        // Fire-and-forget to avoid blocking the watcher thread, but use Semaphore to serialize
        _ = Task.Run(async () =>
        {
            if (!await _processLock.WaitAsync(0)) return; // Already processing
            try
            {
                if (_watchFile == null || !File.Exists(_watchFile)) return;

                _logger.LogInformation("New virtual print job file detected. Waiting for write to complete...");

                // Wait for the file to be readable (unlocked by print spooler)
                if (!await WaitForFileReadyAsync(_watchFile, ct))
                {
                    _logger.LogWarning("Virtual print job file never became readable: {Path}", _watchFile);
                    return;
                }

                _logger.LogInformation("Virtual print job file is ready. Extracting PDF...");

                byte[] pdfBytes;
                try
                {
                    pdfBytes = await File.ReadAllBytesAsync(_watchFile, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read virtual print PDF bytes.");
                    return;
                }

                if (pdfBytes.Length == 0) return;

                var guid = Guid.NewGuid();
                var inboxDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "inbox");
                Directory.CreateDirectory(inboxDir);

                var fileName = $"VirtualPrint_{guid:N}.pdf";
                var filePath = Path.Combine(inboxDir, fileName);

                await File.WriteAllBytesAsync(filePath, pdfBytes, ct);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<IInboxDocumentRepository>();
                    var doc = new InboxDocument
                    {
                        FileName = fileName,
                        PdfPath = filePath,
                        Status = InboxDocumentStatus.Unassigned,
                        ReceivedAt = DateTime.UtcNow
                    };
                    await repo.AddAsync(doc);
                }

                _logger.LogInformation("Successfully imported virtual print job as inbox document {FileName}", fileName);

                // Clean up the original file so it can be recreated/overwritten on next print
                try
                {
                    File.Delete(_watchFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete temporary watch file. Will retry on next tick.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing virtual print job.");
            }
            finally
            {
                _processLock.Release();
            }
        }, ct);
    }

    private static async Task<bool> WaitForFileReadyAsync(string filePath, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            if (ct.IsCancellationRequested) return false;
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                if (stream.Length > 0)
                {
                    return true;
                }
            }
            catch (IOException)
            {
                // File is still being written or locked
            }
            await Task.Delay(500, ct);
        }
        return false;
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        _processLock.Dispose();
        base.Dispose();
    }
}
