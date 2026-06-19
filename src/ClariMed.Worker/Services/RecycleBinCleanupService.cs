using ClariMed.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClariMed.Worker.Services;

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
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        
        var thresholdDate = DateTime.UtcNow.AddDays(-30);

        var oldStudies = await db.Studies
            .IgnoreQueryFilters()
            .AsSplitQuery()
            .Include(s => s.Patient)
            .Include(s => s.SeriesList)
                .ThenInclude(series => series.Images)
            .Where(s => s.IsDeleted && s.DeletedAt < thresholdDate)
            .ToListAsync(stoppingToken);

        if (!oldStudies.Any()) return;

        var archivePath = Path.GetFullPath(config["ClariMed:ArchivePath"] ?? "data/archive");
        var imagesPath = Path.Combine(Path.GetDirectoryName(archivePath) ?? "data", "images");

        foreach (var study in oldStudies)
        {
            _logger.LogInformation("Permanently deleting study {Uid} (Soft deleted at {DeletedAt})", study.StudyInstanceUid, study.DeletedAt);

            // 1. Delete physical DICOM and PNG files
            foreach (var series in study.SeriesList)
            {
                foreach (var img in series.Images)
                {
                    if (!string.IsNullOrEmpty(img.FilePath) && System.IO.File.Exists(img.FilePath))
                    {
                        try { System.IO.File.Delete(img.FilePath); } catch { /* ignore */ }
                        
                        var pngPath = img.FilePath.Replace(".dcm", ".png").Replace("archive", "images");
                        if (System.IO.File.Exists(pngPath))
                        {
                            try { System.IO.File.Delete(pngPath); } catch { /* ignore */ }
                        }
                    }
                }
            }

            // 2. Delete linked Document PDFs
            var docs = await db.Documents.Where(d => d.StudyId == study.Id).ToListAsync(stoppingToken);
            foreach (var doc in docs)
            {
                if (!string.IsNullOrEmpty(doc.PdfFilePath) && System.IO.File.Exists(doc.PdfFilePath))
                {
                    try { System.IO.File.Delete(doc.PdfFilePath); } catch { /* ignore */ }
                }
            }

            db.Studies.Remove(study);
        }

        await db.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Successfully deleted {Count} old studies from the Recycle Bin.", oldStudies.Count);
    }
}
