using ClariMed.Data;
using ClariMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ClariMed.Worker.Services;

/// <summary>
/// Background service that monitors studies in "Receiving" state and marks them
/// as "Complete" once the stabilization window expires (no new files arrived).
///
/// This is purely for status tracking and dashboard visibility.
/// It does NOT create PrintJobs or trigger any printing.
/// </summary>
public class StudyCompletionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StudyCompletionService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    public StudyCompletionService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<StudyCompletionService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stabilizationSeconds = _configuration.GetValue<int>("ClariMed:StudyStabilizationSeconds");
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
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        var cutoff = DateTime.UtcNow - stabilizationWindow;

        var receivingStudies = await db.Studies
            .Where(s => s.Status == StudyStatus.Receiving && s.LastImageReceivedAt < cutoff)
            .Include(s => s.Patient)
            .OrderBy(s => s.LastImageReceivedAt)
            .ToListAsync(ct);

        if (receivingStudies.Count == 0) return;

        foreach (var study in receivingStudies)
        {
            if (ct.IsCancellationRequested) break;

            study.Status = StudyStatus.Complete;
            study.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "✅ Study complete: {Patient} | {Modality} | {ImageCount} image(s) | Study: {Description}",
                study.Patient?.Name ?? "Unknown",
                study.Modality,
                study.ImageCount,
                study.StudyDescription);
        }

        await db.SaveChangesAsync(ct);
    }
}
