using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FocusMed.Documents.Ingestion;

/// <summary>
/// Monitors a local folder for incoming .docx files using <see cref="FileSystemWatcher"/>.
/// When a file is detected, it is queued into the shared <see cref="DocumentIngestionQueue"/>
/// after verifying the file is fully written (retry loop to handle locked files).
/// </summary>
public sealed class WatchFolderIngestionChannel : IDocumentIngestionChannel, IDisposable
{
    private readonly DocumentIngestionQueue _queue;
    private readonly ILogger<WatchFolderIngestionChannel> _logger;
    private readonly string _folderPath;
    private readonly ConcurrentQueue<string> _fileBuffer = new();

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _drainCts;
    private Task? _drainTask;

    /// <summary>Maximum retries when waiting for a file to become readable.</summary>
    private const int MaxRetries = 10;

    /// <summary>Delay between file-lock retries.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public WatchFolderIngestionChannel(
        DocumentIngestionQueue queue,
        ILogger<WatchFolderIngestionChannel> logger,
        string folderPath)
    {
        _queue = queue;
        _logger = logger;
        _folderPath = folderPath;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folderPath);

        _watcher = new FileSystemWatcher(_folderPath, "*.docx")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnFileCreated;
        _watcher.Error += OnWatcherError;

        // Start a background task that drains the file buffer into the channel
        _drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _drainTask = Task.Run(() => DrainBufferAsync(_drainCts.Token), _drainCts.Token);

        _logger.LogInformation("WatchFolder started monitoring: {Path}", _folderPath);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_drainCts != null)
        {
            try
            {
                await _drainCts.CancelAsync();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed by host — safe to ignore
            }

            if (_drainTask != null)
            {
                try { await _drainTask; }
                catch (OperationCanceledException) { /* Expected on shutdown */ }
            }

            try { _drainCts.Dispose(); }
            catch (ObjectDisposedException) { /* Already disposed */ }
        }

        _logger.LogInformation("WatchFolder stopped.");
    }

    /// <summary>
    /// FileSystemWatcher event handler — buffers the path for processing.
    /// Never does DB or I/O work directly in this callback.
    /// </summary>
    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File detected: {Name}", e.Name);
        _fileBuffer.Enqueue(e.FullPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error.");
    }

    /// <summary>
    /// Background loop that drains the file buffer, waits for files to be fully written,
    /// and pushes them into the shared Channel.
    /// </summary>
    private async Task DrainBufferAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_fileBuffer.TryDequeue(out var filePath))
            {
                try
                {
                    // Wait for the file to be fully written (handle locked files)
                    if (!await WaitForFileReadyAsync(filePath, ct))
                    {
                        _logger.LogWarning("File never became readable: {Path}", filePath);
                        continue;
                    }

                    var fileName = Path.GetFileName(filePath);
                    var doc = new IncomingDocument(fileName, filePath, ContentStream: null);

                    await _queue.Writer.WriteAsync(doc, ct);
                    _logger.LogInformation("Queued document for processing: {Name}", fileName);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enqueue file: {Path}", filePath);
                }
            }
            else
            {
                // Nothing in the buffer — wait a bit before checking again
                await Task.Delay(200, ct);
            }
        }
    }

    /// <summary>
    /// Waits for a file to be fully written and not locked by another process.
    /// Returns true if the file is ready, false if it times out.
    /// </summary>
    private static async Task<bool> WaitForFileReadyAsync(string filePath, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // Try opening with exclusive read — if it succeeds, the file is ready
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                // File is still being written — wait and retry
                await Task.Delay(RetryDelay, ct);
            }
        }

        return false;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _drainCts?.Dispose();
    }
}
