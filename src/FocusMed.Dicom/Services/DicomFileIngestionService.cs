using FocusMed.Data;
using FocusMed.Data.Models;
using FellowOakDicom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FocusMed.Dicom.Services;

public class DicomFileIngestionService
{
    private readonly IServiceProvider _rootProvider;
    private readonly ILogger<DicomFileIngestionService> _logger;
    private readonly string _archivePath;
    private readonly string _imagesPath;

    public DicomFileIngestionService(
        IServiceProvider serviceProvider,
        ILogger<DicomFileIngestionService> logger)
    {
        _rootProvider = serviceProvider;
        _logger = logger;
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        _archivePath = Path.GetFullPath(config["FocusMed:ArchivePath"] ?? "data/archive");
        var rootDataPath = Path.GetDirectoryName(_archivePath) ?? "data";
        _imagesPath = config["FocusMed:ImagesPath"] ?? Path.Combine(rootDataPath, "images");
    }

    public async Task<(int Success, int Failed)> IngestFolderAsync(string folderPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning("Folder not found: {Path}", folderPath);
            return (0, 0);
        }

        var files = Directory.GetFiles(folderPath, "*.dcm", SearchOption.TopDirectoryOnly);
        int success = 0, failed = 0;

        foreach (var file in files)
        {
            try
            {
                await IngestSingleFileAsync(file, ct);
                success++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest DCM file: {Path}", file);
                failed++;
            }
        }

        _logger.LogInformation("DCM ingestion complete — {Success} succeeded, {Failed} failed", success, failed);
        return (success, failed);
    }

    private async Task IngestSingleFileAsync(string filePath, CancellationToken ct)
    {
        var dcmFile = await DicomFile.OpenAsync(filePath);
        var dataset = dcmFile.Dataset;

        var tags = DicomUpsertService.ExtractTags(dataset);
        var pathParts = DicomUpsertService.GeneratePaths(tags);

        using var scope = _rootProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

        var studyLock = DicomUpsertService.GetStudyLock(tags.StudyUid);
        await studyLock.WaitAsync(ct);
        try
        {
            var (patient, study, series) = await DicomUpsertService.UpsertEntitiesAsync(db, tags, ct);

            if (DicomUpsertService.IsDuplicate(db, tags.SopUid, ct))
            {
                _logger.LogInformation("Duplicate SOP {SopUid} — skipping", tags.SopUid);
                return;
            }

            var dcmPath = Path.Combine(_archivePath, pathParts[0], pathParts[1], pathParts[2], $"{pathParts[3]}.dcm");

            await DicomUpsertService.CopyDcmFileAsync(filePath, dcmPath, ct);

            DicomUpsertService.ConvertToPng(dataset, "", tags.Rows, tags.Columns, _imagesPath, pathParts, _logger);

            var dicomImage = new FocusMed.Data.Models.DicomImage
            {
                SopInstanceUid = tags.SopUid,
                SeriesId = series.Id,
                InstanceNumber = tags.InstanceNumber,
                FilePath = dcmPath,
                Rows = tags.Rows,
                Columns = tags.Columns,
                FrameCount = tags.FrameCount
            };
            db.Images.Add(dicomImage);

            study.LastImageReceivedAt = DateTime.UtcNow;
            study.ImageCount = await db.Images
                .CountAsync(i => db.Series.Where(s => s.StudyId == study.Id)
                .Select(s => s.Id).Contains(i.SeriesId), ct);
            study.Status = StudyStatus.Receiving;

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            studyLock.Release();
        }

        _logger.LogInformation("Ingested DCM: {Patient} | {Modality} | {Instance} ({Frames} frames)",
            tags.PatientName, tags.Modality, pathParts[3], tags.FrameCount);
    }
}
