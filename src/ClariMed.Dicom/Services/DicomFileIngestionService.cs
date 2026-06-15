using System.Text;
using ClariMed.Data;
using ClariMed.Data.Models;
using ClariMed.Imaging.Converters;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClariMed.Dicom.Services;

public class DicomFileIngestionService
{
    private readonly IServiceProvider _rootProvider;
    private readonly ILogger<DicomFileIngestionService> _logger;
    private readonly string _archivePath;
    private readonly string _imagesPath;
    private static readonly SemaphoreSlim _dbLock = new(1, 1);

    public DicomFileIngestionService(
        IServiceProvider serviceProvider,
        ILogger<DicomFileIngestionService> logger)
    {
        _rootProvider = serviceProvider;
        _logger = logger;
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        _archivePath = Path.GetFullPath(config["ClariMed:ArchivePath"] ?? "data/archive");
        var rootDataPath = Path.GetDirectoryName(_archivePath) ?? "data";
        _imagesPath = Path.Combine(rootDataPath, "images");
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

        // ── Extract tags ──
        var patientIdTag = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
        if (string.IsNullOrWhiteSpace(patientIdTag))
            patientIdTag = $"UNKNOWN-{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        var rawPatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown");
        var patientName = rawPatientName.Replace("^", " ").Replace("  ", " ").Trim();
        if (string.IsNullOrWhiteSpace(patientName)) patientName = "Unknown";
        if (string.IsNullOrWhiteSpace(patientName)) patientName = "Unknown";

        var patientSex = dataset.GetSingleValueOrDefault(DicomTag.PatientSex, "");
        var birthDateStr = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "");
        var studyUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, Guid.NewGuid().ToString());
        var seriesUid = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, Guid.NewGuid().ToString());
        var sopUid = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, Guid.NewGuid().ToString());
        var accession = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "");
        var studyDesc = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, "");
        var seriesDesc = dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, "");
        var modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, "OT");
        var studyDateStr = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "");
        var referringPhysician = dataset.GetSingleValueOrDefault(DicomTag.ReferringPhysicianName, "").Replace("^", " ").Trim();
        var institution = dataset.GetSingleValueOrDefault(DicomTag.InstitutionName, "");
        var manufacturer = dataset.GetSingleValueOrDefault(DicomTag.Manufacturer, "");
        var stationName = dataset.GetSingleValueOrDefault(DicomTag.StationName, "");

        int? instanceNumber = dataset.TryGetSingleValue<int>(DicomTag.InstanceNumber, out var iNum) ? iNum : null;
        int? seriesNumber = dataset.TryGetSingleValue<int>(DicomTag.SeriesNumber, out var sNum) ? sNum : null;
        int? rows = dataset.TryGetSingleValue<int>(DicomTag.Rows, out var rNum) ? rNum : null;
        int? columns = dataset.TryGetSingleValue<int>(DicomTag.Columns, out var cNum) ? cNum : null;
        int frameCount = dataset.TryGetSingleValue<int>(DicomTag.NumberOfFrames, out var fNum) ? fNum : 1;

        DateTime? studyDate = DateTime.TryParseExact(studyDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;
        DateTime? birthDate = DateTime.TryParseExact(birthDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var bParsed) ? bParsed : null;

        using var scope = _rootProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        Patient? patient;
        Study? study;
        Data.Models.Series? series;

        await _dbLock.WaitAsync(ct);
        try
        {
            // ── Upsert Patient ──
            patient = await db.Patients.FirstOrDefaultAsync(p => p.PatientId == patientIdTag, ct);
            if (patient == null)
            {
                patient = new Patient
                {
                    PatientId = patientIdTag,
                    Name = patientName,
                    Sex = patientSex,
                    BirthDate = birthDate
                };
                db.Patients.Add(patient);
                await db.SaveChangesAsync(ct);
            }
            else if (patient.Name == "Unknown" && patientName != "Unknown")
            {
                patient.Name = patientName;
                await db.SaveChangesAsync(ct);
            }

            // ── Upsert Study ──
            study = await db.Studies.FirstOrDefaultAsync(s => s.StudyInstanceUid == studyUid, ct);
            if (study == null)
            {
                study = new Study
                {
                    StudyInstanceUid = studyUid,
                    PatientId = patient.Id,
                    AccessionNumber = accession,
                    StudyDescription = studyDesc,
                    Modality = modality,
                    StudyDate = studyDate,
                    ReferringPhysicianName = referringPhysician,
                    InstitutionName = institution,
                    Status = StudyStatus.Receiving,
                    LastImageReceivedAt = DateTime.UtcNow
                };
                db.Studies.Add(study);
                await db.SaveChangesAsync(ct);
            }

            // ── Upsert Series ──
            series = await db.Series.FirstOrDefaultAsync(s => s.SeriesInstanceUid == seriesUid, ct);
            if (series == null)
            {
                series = new Data.Models.Series
                {
                    SeriesInstanceUid = seriesUid,
                    StudyId = study.Id,
                    SeriesNumber = seriesNumber,
                    Modality = modality,
                    SeriesDescription = seriesDesc,
                    Manufacturer = manufacturer,
                    StationName = stationName
                };
                db.Series.Add(series);
                await db.SaveChangesAsync(ct);
            }
        }
        finally
        {
            _dbLock.Release();
        }

        // ── Check duplicate ──
        var existingImage = await db.Images.AnyAsync(i => i.SopInstanceUid == sopUid, ct);
        if (existingImage)
        {
            _logger.LogInformation("Duplicate SOP {SopUid} — skipping", sopUid);
            return;
        }

        // ── Generate paths ──
        var safePatient = $"{Sanitize(patientName)}_{Sanitize(patientIdTag)}";
        var safeStudy = $"{studyDate?.ToString("yyyy-MM-dd") ?? "NoDate"}_{Sanitize(modality)}_{Math.Abs(GetStableHashCode(studyUid)):X4}";
        var safeSeries = $"Series_{seriesNumber?.ToString() ?? "0"}";
        var timestamp = DateTime.Now.ToString("HHmm");
        var safeInstance = $"IMG_{instanceNumber?.ToString() ?? "0"}-{timestamp}";

        // ── Save .dcm ──
        var storageDir = Path.Combine(_archivePath, safePatient, safeStudy, safeSeries);
        Directory.CreateDirectory(storageDir);
        var dcmPath = Path.Combine(storageDir, $"{safeInstance}.dcm");

        // Copy the source file to archive
        File.Copy(filePath, dcmPath, overwrite: true);

        // ── Convert pixel data to PNG (Cache) ──
        string? pngPath = null;

        if (rows.HasValue && columns.HasValue)
        {
            try
            {
                var imageDir = Path.Combine(_imagesPath, safePatient, safeStudy, safeSeries);
                Directory.CreateDirectory(imageDir);
                pngPath = Path.Combine(imageDir, $"{safeInstance}.png");

                var foDicomImage = new FellowOakDicom.Imaging.DicomImage(dataset);
                frameCount = foDicomImage.NumberOfFrames;

                for (int i = 0; i < frameCount; i++)
                {
                    var framePath = frameCount > 1
                        ? pngPath.Replace(".png", $"_f{i}.png")
                        : pngPath;

                    using var bitmap = foDicomImage.RenderImage(i).As<System.Drawing.Bitmap>();
                    bitmap.Save(framePath, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not convert pixel data for {FilePath}", filePath);
            }
        }

        // ── Save DicomImage record ──
        await _dbLock.WaitAsync(ct);
        try
        {
            var dicomImage = new ClariMed.Data.Models.DicomImage
            {
                SopInstanceUid = sopUid,
                SeriesId = series.Id,
                InstanceNumber = instanceNumber,
                FilePath = dcmPath,
                Rows = rows,
                Columns = columns,
                FrameCount = frameCount
            };
            db.Images.Add(dicomImage);
            await db.SaveChangesAsync(ct);

            // ── Update study receiving state ──
            study.LastImageReceivedAt = DateTime.UtcNow;
            study.ImageCount = await db.Images
                .CountAsync(i => db.Series.Where(s => s.StudyId == study.Id)
                .Select(s => s.Id).Contains(i.SeriesId), ct);
            study.Status = StudyStatus.Receiving;
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            _dbLock.Release();
        }

        _logger.LogInformation("Ingested DCM: {Patient} | {Modality} | {Instance} ({Frames} frames)", patientName, modality, safeInstance, frameCount);
    }

    private static int GetStableHashCode(string str)
    {
        unchecked
        {
            int hash = (int)2166136261;
            foreach (char c in str)
            {
                hash = (hash ^ c) * 16777619;
            }
            return hash;
        }
    }

    private static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Unknown";
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(input.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray()).Trim();
    }
}