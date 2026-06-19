using FocusMed.Documents.Ingestion;

namespace FocusMed.Worker.Services;

/// <summary>
/// Background service that starts all registered <see cref="IDocumentIngestionChannel"/>
/// producers on application startup and shuts them down gracefully on stop.
/// Currently starts the WatchFolder channel; future channels (API, etc.)
/// are added here without changing any downstream logic.
/// </summary>
public class DocumentWatcherService : BackgroundService
{
    private readonly IEnumerable<IDocumentIngestionChannel> _channels;
    private readonly ILogger<DocumentWatcherService> _logger;

    public DocumentWatcherService(
        IEnumerable<IDocumentIngestionChannel> channels,
        ILogger<DocumentWatcherService> logger)
    {
        _channels = channels;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentWatcherService starting — {Count} ingestion channel(s) registered.",
            _channels.Count());

        // Start all registered channels
        foreach (var channel in _channels)
        {
            try
            {
                await channel.StartAsync(stoppingToken);
                _logger.LogInformation("Started ingestion channel: {Type}", channel.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start ingestion channel: {Type}", channel.GetType().Name);
            }
        }

        // Keep running until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        // Stop all channels
        foreach (var channel in _channels)
        {
            try
            {
                await channel.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping ingestion channel: {Type}", channel.GetType().Name);
            }
        }

        _logger.LogInformation("DocumentWatcherService stopped.");
    }
}
