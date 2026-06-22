using FocusMed.Data.Services;
using FocusMed.Dicom.Services;

namespace FocusMed.Worker.Services;

/// <summary>
/// Background service that starts the DICOM C-STORE SCP server on application startup
/// and shuts it down gracefully on stop.
/// </summary>
public class DicomListenerService : BackgroundService
{
    private readonly IDicomServer _dicomServer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DicomListenerService> _logger;

    public DicomListenerService(
        IDicomServer dicomServer,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DicomListenerService> logger)
    {
        _dicomServer = dicomServer;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configSection = _configuration.GetSection("FocusMed");
        var aeTitle = configSection.GetValue<string>("AETitle") ?? "FOCUSMED";
        var port = configSection.GetValue<int>("DicomPort");
        if (port == 0) port = 1004;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
            var settings = await settingsRepo.GetAsync();

            if (!string.IsNullOrWhiteSpace(settings.AETitle)) aeTitle = settings.AETitle;
            if (settings.DicomPort > 0) port = settings.DicomPort;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load settings from database — using appsettings.json values.");
        }

        _logger.LogInformation("FocusMed DICOM Listener starting — AE Title: {AETitle}, Port: {Port}", aeTitle, port);

        try
        {
            await _dicomServer.StartAsync(aeTitle, port, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start DICOM server on port {Port}.", port);
            return;
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        await _dicomServer.StopAsync();
        _logger.LogInformation("FocusMed DICOM Listener stopped.");
    }
}
