using FocusMed.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusMed.Worker.Services;

public class RecycleBinCleanupService : BackgroundService
{
    private readonly ILogger<RecycleBinCleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _pollInterval = TimeSpan.FromHours(12);

    public RecycleBinCleanupService(
        ILogger<RecycleBinCleanupService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecycleBinCleanupService started — checking every {Interval} hours.", _pollInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldStudiesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Recycle Bin cleanup.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task CleanupOldStudiesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var thresholdDate = DateTime.UtcNow.AddDays(-30);

        // Get study IDs first (lightweight query)
        var studyIds = await db.Studies
            .IgnoreQueryFilters()
            .Where(s => s.IsDeleted && s.DeletedAt < thresholdDate)
            .Select(s => s.Id)
            .ToListAsync(stoppingToken);

        if (studyIds.Count == 0) return;

        var archivePath = Path.GetFullPath(config["FocusMed:ArchivePath"] ?? "data/archive");

        // Pre-fetch all documents for these studies (single query, no N+1)
        var allDocPaths = await db.Documents
            .AsNoTracking()
            .Where(d => studyIds.Contains(d.StudyId ?? 0) && d.PdfFilePath != null)
            .Select(d => d.PdfFilePath!)
            .ToListAsync(stoppingToken);

        // Delete document PDFs
        foreach (var docPath in allDocPaths)
        {
            if (System.IO.File.Exists(docPath))
            {
                try { System.IO.File.Delete(docPath); } catch { }
            }
        }

        // Load studies with minimal includes for file cleanup
        var oldStudies = await db.Studies
            .IgnoreQueryFilters()
            .Include(s => s.SeriesList)
                .ThenInclude(series => series.Images)
            .Where(s => studyIds.Contains(s.Id))
            .ToListAsync(stoppingToken);

        foreach (var study in oldStudies)
        {
            _logger.LogInformation("Permanently deleting study {Uid} (Soft deleted at {DeletedAt})", study.StudyInstanceUid, study.DeletedAt);

            foreach (var series in study.SeriesList)
            {
                foreach (var img in series.Images)
                {
                    if (!string.IsNullOrEmpty(img.FilePath) && System.IO.File.Exists(img.FilePath))
                    {
                        try { System.IO.File.Delete(img.FilePath); } catch { }

                        var pngPath = img.FilePath.Replace(".dcm", ".png").Replace("archive", "images");
                        if (System.IO.File.Exists(pngPath))
                        {
                            try { System.IO.File.Delete(pngPath); } catch { }
                        }
                    }
                }
            }

            db.Studies.Remove(study);
        }

        await db.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Successfully deleted {Count} old studies from the Recycle Bin.", oldStudies.Count);
    }
}
