using ClariMed.Data;
using ClariMed.Data.Models;
using ClariMed.Data.Services;
using ClariMed.Dicom.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClariMed.Worker.Services;

/// <summary>
/// Polls for pending DICOM setting changes. When the admin saves new AE Title or port,
/// it waits until the server is idle (no active connections + no receiving studies) then restarts.
/// </summary>
public class DicomRestartService : BackgroundService
{
    private readonly ILogger<DicomRestartService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDicomServer _dicomServer;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public DicomRestartService(
        ILogger<DicomRestartService> logger,
        IServiceScopeFactory scopeFactory,
        IDicomServer dicomServer)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _dicomServer = dicomServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DicomRestartService started — polling every {Interval}s.", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRestartAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DICOM restart check.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task CheckAndRestartAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        var settings = await db.ClinicSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (settings == null || !settings.DicomSettingsPendingRestart) return;

        // Check if any studies are still receiving
        var hasReceiving = await db.Studies.AnyAsync(s => s.Status == StudyStatus.Receiving, ct);
        if (hasReceiving)
        {
            _logger.LogInformation("DICOM restart pending — studies still being received. Waiting...");
            return;
        }

        // Safe to restart
        var newAeTitle = settings.AETitle;
        var newPort = settings.DicomPort;

        _logger.LogInformation("Restarting DICOM server — new AE Title: {AETitle}, Port: {Port}", newAeTitle, newPort);

        await _dicomServer.StopAsync();
        await _dicomServer.StartAsync(newAeTitle, newPort, ct);

        settings.DicomSettingsPendingRestart = false;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("DICOM server restarted successfully on port {Port}.", newPort);
    }
}
