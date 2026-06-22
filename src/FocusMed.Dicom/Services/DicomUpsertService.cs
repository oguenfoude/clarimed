using System.Collections.Concurrent;
using System.Text;
using FocusMed.Data;
using FocusMed.Data.Models;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FocusMed.Dicom.Services;

/// <summary>
/// Shared DICOM ingestion logic used by both CStoreScp (network) and DicomFileIngestionService (folder).
/// Per-study locking allows parallel ingestion of different studies while serializing images within the same study.
/// </summary>
public class DicomUpsertService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _studyLocks = new();
    private static readonly char[] _invalidChars = Path.GetInvalidFileNameChars();

    private readonly string _archivePath;
    private readonly string _imagesPath;
    private readonly ILogger _logger;

    public DicomUpsertService(IConfiguration config, ILogger<DicomUpsertService> logger)
    {
        _archivePath = Path.GetFullPath(config["FocusMed:ArchivePath"] ?? "data/archive");
        var rootDataPath = Path.GetDirectoryName(_archivePath) ?? "data";
        _imagesPath = config["FocusMed:ImagesPath"] ?? Path.Combine(rootDataPath, "images");
        _logger = logger;
    }

    public class DicomTags
    {
        public string PatientId = string.Empty;
        public string PatientName = string.Empty;
        public string PatientSex = string.Empty;
        public string BirthDateStr = string.Empty;
        public string StudyUid = string.Empty;
        public string SeriesUid = string.Empty;
        public string SopUid = string.Empty;
        public string Accession = string.Empty;
        public string StudyDesc = string.Empty;
        public string SeriesDesc = string.Empty;
        public string Modality = "OT";
        public string StudyDateStr = string.Empty;
        public string ReferringPhysician = string.Empty;
        public string Institution = string.Empty;
        public string Manufacturer = string.Empty;
        public string StationName = string.Empty;
        public int? InstanceNumber;
        public int? SeriesNumber;
        public int? Rows;
        public int? Columns;
        public int FrameCount = 1;
        public DateTime? StudyDate;
        public DateTime? BirthDate;
    }

    public static DicomTags ExtractTags(DicomDataset dataset)
    {
        var tags = new DicomTags();

        tags.PatientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
        if (string.IsNullOrWhiteSpace(tags.PatientId))
            tags.PatientId = $"UNKNOWN-{Guid.NewGuid():N}".Substring(0, 15);

        var rawPatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown");
        tags.PatientName = rawPatientName.Replace("^", " ").Replace("  ", " ").Trim();
        if (string.IsNullOrWhiteSpace(tags.PatientName)) tags.PatientName = "Unknown";

        tags.PatientSex = dataset.GetSingleValueOrDefault(DicomTag.PatientSex, "");
        tags.BirthDateStr = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "");
        tags.StudyUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, Guid.NewGuid().ToString());
        tags.SeriesUid = dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, Guid.NewGuid().ToString());
        tags.SopUid = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, Guid.NewGuid().ToString());
        tags.Accession = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "");
        tags.StudyDesc = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, "");
        tags.SeriesDesc = dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, "");
        tags.Modality = dataset.GetSingleValueOrDefault(DicomTag.Modality, "OT");
        tags.StudyDateStr = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "");
        tags.ReferringPhysician = dataset.GetSingleValueOrDefault(DicomTag.ReferringPhysicianName, "").Replace("^", " ").Trim();
        tags.Institution = dataset.GetSingleValueOrDefault(DicomTag.InstitutionName, "");
        tags.Manufacturer = dataset.GetSingleValueOrDefault(DicomTag.Manufacturer, "");
        tags.StationName = dataset.GetSingleValueOrDefault(DicomTag.StationName, "");

        tags.InstanceNumber = dataset.TryGetSingleValue<int>(DicomTag.InstanceNumber, out var iNum) ? iNum : null;
        tags.SeriesNumber = dataset.TryGetSingleValue<int>(DicomTag.SeriesNumber, out var sNum) ? sNum : null;
        tags.Rows = dataset.TryGetSingleValue<int>(DicomTag.Rows, out var rNum) ? rNum : null;
        tags.Columns = dataset.TryGetSingleValue<int>(DicomTag.Columns, out var cNum) ? cNum : null;
        tags.FrameCount = dataset.TryGetSingleValue<int>(DicomTag.NumberOfFrames, out var fNum) ? fNum : 1;

        if (DateTime.TryParseExact(tags.StudyDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
            tags.StudyDate = parsed;
        if (DateTime.TryParseExact(tags.BirthDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var bParsed))
            tags.BirthDate = bParsed;

        return tags;
    }

    /// <summary>
    /// Upserts Patient, Study, and Series in a single SaveChangesAsync call.
    /// Returns (patient, study, series).
    /// </summary>
    public static async Task<(Patient patient, Study study, Data.Models.Series series)> UpsertEntitiesAsync(
        FocusMedDbContext db, DicomTags tags, CancellationToken ct = default)
    {
        // Upsert Patient
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.PatientId == tags.PatientId, ct);
        if (patient == null)
        {
            patient = new Patient
            {
                PatientId = tags.PatientId,
                Name = tags.PatientName,
                Sex = tags.PatientSex,
                BirthDate = tags.BirthDate
            };
            db.Patients.Add(patient);
        }
        else if (patient.Name == "Unknown" && tags.PatientName != "Unknown")
        {
            patient.Name = tags.PatientName;
        }

        // Upsert Study
        var study = await db.Studies.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.StudyInstanceUid == tags.StudyUid, ct);
        if (study == null)
        {
            study = new Study
            {
                StudyInstanceUid = tags.StudyUid,
                PatientId = patient.Id,
                AccessionNumber = tags.Accession,
                StudyDescription = tags.StudyDesc,
                Modality = tags.Modality,
                StudyDate = tags.StudyDate,
                ReferringPhysicianName = tags.ReferringPhysician,
                InstitutionName = tags.Institution,
                Status = StudyStatus.Receiving,
                LastImageReceivedAt = DateTime.UtcNow
            };
            db.Studies.Add(study);
        }
        else if (study.IsDeleted)
        {
            study.IsDeleted = false;
            study.DeletedAt = null;
            study.Status = StudyStatus.Receiving;
            study.LastImageReceivedAt = DateTime.UtcNow;
        }

        // Upsert Series
        var series = await db.Series.FirstOrDefaultAsync(s => s.SeriesInstanceUid == tags.SeriesUid, ct);
        if (series == null)
        {
            series = new Data.Models.Series
            {
                SeriesInstanceUid = tags.SeriesUid,
                StudyId = study.Id,
                SeriesNumber = tags.SeriesNumber,
                Modality = tags.Modality,
                SeriesDescription = tags.SeriesDesc,
                Manufacturer = tags.Manufacturer,
                StationName = tags.StationName
            };
            db.Series.Add(series);
        }

        // Single SaveChanges for all three upserts
        await db.SaveChangesAsync(ct);
        return (patient, study, series);
    }

    public static string[] GeneratePaths(DicomTags tags)
    {
        var safePatient = $"{Sanitize(tags.PatientName)}_{Sanitize(tags.PatientId)}";
        var safeStudy = $"{tags.StudyDate?.ToString("yyyy-MM-dd") ?? "NoDate"}_{Sanitize(tags.Modality)}_{Math.Abs(GetStableHashCode(tags.StudyUid)):X4}";
        var safeSeries = $"Series_{tags.SeriesNumber?.ToString() ?? "0"}";
        var timestamp = DateTime.UtcNow.ToString("HHmm");
        var safeInstance = $"IMG_{tags.InstanceNumber?.ToString() ?? "0"}-{timestamp}";

        return new[] { safePatient, safeStudy, safeSeries, safeInstance };
    }

    public static bool IsDuplicate(FocusMedDbContext db, string sopUid, CancellationToken ct = default)
    {
        return db.Images.Any(i => i.SopInstanceUid == sopUid);
    }

    public static async Task SaveDcmFileAsync(string dcmPath, DicomFile dcmFile, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(dcmPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await dcmFile.SaveAsync(dcmPath);
    }

    public static async Task CopyDcmFileAsync(string sourcePath, string dcmPath, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(dcmPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await using var sourceStream = File.OpenRead(sourcePath);
        await using var destStream = File.Create(dcmPath);
        await sourceStream.CopyToAsync(destStream, ct);
    }

    public static string[] ConvertToPng(DicomDataset dataset, string pngPath, int? rows, int? columns, string imagesPath, string[] pathParts, ILogger logger)
    {
        var savedPaths = new List<string>();
        if (!rows.HasValue || !columns.HasValue) return savedPaths.ToArray();

        try
        {
            var imageDir = Path.Combine(imagesPath, pathParts[0], pathParts[1], pathParts[2]);
            Directory.CreateDirectory(imageDir);
            var finalPngPath = Path.Combine(imageDir, $"{pathParts[3]}.png");

            var foImage = new FellowOakDicom.Imaging.DicomImage(dataset);
            var frameCount = foImage.NumberOfFrames;

            for (int i = 0; i < frameCount; i++)
            {
                var framePath = frameCount > 1
                    ? finalPngPath.Replace(".png", $"_f{i}.png")
                    : finalPngPath;

                using var bitmap = foImage.RenderImage(i).As<System.Drawing.Bitmap>();
                bitmap.Save(framePath, System.Drawing.Imaging.ImageFormat.Png);
                savedPaths.Add(framePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not convert pixel data for SOP {SopUid}", pathParts[3]);
        }

        return savedPaths.ToArray();
    }

    public static async Task<FocusMed.Data.Models.DicomImage> SaveImageRecordAsync(
        FocusMedDbContext db, string sopUid, int seriesId, int? instanceNumber, string dcmPath,
        int? rows, int? columns, int frameCount, CancellationToken ct = default)
    {
        var dicomImage = new FocusMed.Data.Models.DicomImage
        {
            SopInstanceUid = sopUid,
            SeriesId = seriesId,
            InstanceNumber = instanceNumber,
            FilePath = dcmPath,
            Rows = rows,
            Columns = columns,
            FrameCount = frameCount
        };
        db.Images.Add(dicomImage);
        await db.SaveChangesAsync(ct);
        return dicomImage;
    }

    public static async Task UpdateStudyImageCountAsync(FocusMedDbContext db, Study study, CancellationToken ct = default)
    {
        study.LastImageReceivedAt = DateTime.UtcNow;
        study.ImageCount = await db.Images
            .CountAsync(i => db.Series.Where(s => s.StudyId == study.Id)
            .Select(s => s.Id).Contains(i.SeriesId), ct);
        study.Status = StudyStatus.Receiving;
        await db.SaveChangesAsync(ct);
    }

    public static SemaphoreSlim GetStudyLock(string studyUid)
    {
        return _studyLocks.GetOrAdd(studyUid, _ => new SemaphoreSlim(1, 1));
    }

    public static void ReleaseStudyLock(string studyUid)
    {
        if (_studyLocks.TryRemove(studyUid, out var sem))
            sem.Dispose();
    }

    public static int GetStableHashCode(string str)
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

    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Unknown";
        return new string(input.Select(c => Array.IndexOf(_invalidChars, c) >= 0 ? '_' : c).ToArray()).Trim();
    }
}
