using FocusMed.Data;
using FocusMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Worker.Services;

/// <summary>
/// Background service that monitors studies in "Receiving" state and marks them
/// as "Complete" once the stabilization window expires (no new files arrived).
/// </summary>
public class StudyCompletionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StudyCompletionService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    public StudyCompletionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<StudyCompletionService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stabilizationSeconds = _configuration.GetValue<int>("FocusMed:StudyStabilizationSeconds");
        if (stabilizationSeconds <= 0) stabilizationSeconds = 30;
        var stabilizationWindow = TimeSpan.FromSeconds(stabilizationSeconds);

        _logger.LogInformation(
            "StudyCompletionService started — stabilization window: {Seconds}s, polling every {Poll}s.",
            stabilizationSeconds, PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForCompletedStudiesAsync(stabilizationWindow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during study completion check.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("StudyCompletionService stopped.");
    }

    private async Task CheckForCompletedStudiesAsync(TimeSpan stabilizationWindow, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

        var cutoff = DateTime.UtcNow - stabilizationWindow;

        var receivingStudies = await db.Studies
            .Where(s => s.Status == StudyStatus.Receiving && s.LastImageReceivedAt < cutoff)
            .OrderBy(s => s.LastImageReceivedAt)
            .ToListAsync(ct);

        if (receivingStudies.Count == 0) return;

        foreach (var study in receivingStudies)
        {
            if (ct.IsCancellationRequested) break;

            study.Status = StudyStatus.Complete;
            study.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Study complete: {StudyUid} | {Modality} | {ImageCount} image(s) | Study: {Description}",
                study.StudyInstanceUid,
                study.Modality,
                study.ImageCount,
                study.StudyDescription);
        }

        await db.SaveChangesAsync(ct);
    }
}
