using System.Text;
using ClariMed.Data;
using ClariMed.Data.Models;
using ClariMed.Data.Services;
using ClariMed.Imaging.Converters;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ClariMed.Dicom.Handlers;

/// <summary>
/// DICOM C-STORE SCP handler. Receives DICOM images from modalities and:
///   1. Saves the raw .dcm file to disk
///   2. Extracts DICOM tags and persists Patient → Study → Series → DicomImage
///   3. Converts pixel data to PNG
///   4. Generates a thumbnail
/// </summary>
public class CStoreScp : DicomService, IDicomServiceProvider, IDicomCStoreProvider
{
    private static readonly DicomTransferSyntax[] AcceptedTransferSyntaxes = new[]
    {
        // Uncompressed
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.ExplicitVRBigEndian,
        DicomTransferSyntax.ImplicitVRLittleEndian,
        // JPEG compressed
        DicomTransferSyntax.JPEGProcess1,           // JPEG Baseline (Process 1)
        DicomTransferSyntax.JPEGProcess2_4,         // JPEG Extended (Process 2 & 4)
        DicomTransferSyntax.JPEGProcess14,          // JPEG Lossless (Process 14)
        DicomTransferSyntax.JPEGProcess14SV1,       // JPEG Lossless SV1 (Process 14, Selection Value 1)
        // JPEG 2000
        DicomTransferSyntax.JPEG2000Lossless,
        DicomTransferSyntax.JPEG2000Lossy,
        // RLE
        DicomTransferSyntax.RLELossless,
    };

    private readonly IServiceProvider _rootProvider;
    private readonly ILogger<CStoreScp> _logger;
    private readonly string _basePath;
    private readonly string _imagesPath;
    private static readonly SemaphoreSlim _dbLock = new(1, 1);

    public CStoreScp(
        INetworkStream stream,
        Encoding fallbackEncoding,
        ILogger logger,
        DicomServiceDependencies dependencies,
        IServiceProvider serviceProvider)
        : base(stream, fallbackEncoding, logger, dependencies)
    {
        _rootProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<CStoreScp>>();
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        _basePath = config["ClariMed:ArchivePath"] ?? "data/archive";
        _basePath = Path.GetFullPath(_basePath);
        // Go up one level from 'archive' to find the 'images' folder
        var rootDataPath = Path.GetDirectoryName(_basePath) ?? "data";
        _imagesPath = Path.Combine(rootDataPath, "images");
    }

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        foreach (var pc in association.PresentationContexts)
        {
            pc.AcceptTransferSyntaxes(AcceptedTransferSyntaxes);
        }

        return SendAssociationAcceptAsync(association);
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
    {
        return SendAssociationReleaseResponseAsync();
    }

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
    {
        _logger.LogWarning("DICOM abort received — Source: {Source}, Reason: {Reason}", source, reason);
    }

    public void OnConnectionClosed(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "DICOM connection closed with error.");
        }
    }

    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        try
        {
            // Create a new scope for database operations
            using var scope = _rootProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
            var imageConverter = scope.ServiceProvider.GetRequiredService<IImageConverter>();

            var dataset = request.Dataset;

            // ── Extract DICOM tags Safely ──
            var patientIdTag = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
            if (string.IsNullOrWhiteSpace(patientIdTag))
            {
                patientIdTag = $"UNKNOWN-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            }

            var rawPatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown");
            // DICOM names are separated by '^'. Replace with spaces for display.
            var patientName = rawPatientName.Replace("^", " ").Replace("  ", " ").Trim();
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

            DateTime? studyDate = null;
            if (DateTime.TryParseExact(studyDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
                studyDate = parsed;

            DateTime? birthDate = null;
            if (DateTime.TryParseExact(birthDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var bParsed))
                birthDate = bParsed;

            Patient? patient;
            Study? study;
            Data.Models.Series? series;

            await _dbLock.WaitAsync();
            try
            {
                // ── Upsert Patient ──
                patient = await db.Patients.FirstOrDefaultAsync(p => p.PatientId == patientIdTag);
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
                    await db.SaveChangesAsync();
                }
                else
                {
                    // Update Unknown names if a better one is found
                    if (patient.Name == "Unknown" && patientName != "Unknown")
                    {
                        patient.Name = patientName;
                        await db.SaveChangesAsync();
                    }
                }

                // ── Upsert Study ──
                study = await db.Studies.FirstOrDefaultAsync(s => s.StudyInstanceUid == studyUid);
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
                    await db.SaveChangesAsync();
                }

                // ── Upsert Series ──
                series = await db.Series.FirstOrDefaultAsync(s => s.SeriesInstanceUid == seriesUid);
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
                    await db.SaveChangesAsync();
                }
            }
            finally
            {
                _dbLock.Release();
            }

            // ── Check for duplicate SOP Instance ──
            var existingImage = await db.Images.AnyAsync(i => i.SopInstanceUid == sopUid);
            if (existingImage)
            {
                _logger.LogInformation("Duplicate SOP Instance UID {SopUid} — already stored, skipping.", sopUid);
                return new DicomCStoreResponse(request, DicomStatus.Success);
            }

            // ── Generate Human-Readable & Safe File Paths ──
            var safePatient = $"{Sanitize(patientName)}_{Sanitize(patientIdTag)}";
            var safeStudy = $"{studyDate?.ToString("yyyy-MM-dd") ?? "NoDate"}_{Sanitize(modality)}_{Math.Abs(GetStableHashCode(studyUid)):X4}";
            var safeSeries = $"Series_{seriesNumber?.ToString() ?? "0"}";
            var timestamp = DateTime.Now.ToString("HHmm");
            var safeInstance = $"IMG_{instanceNumber?.ToString() ?? "0"}-{timestamp}";

            // ── Save .dcm file to disk (Archive) ──
            var storageDir = Path.Combine(_basePath, safePatient, safeStudy, safeSeries);
            Directory.CreateDirectory(storageDir);
            var dcmPath = Path.Combine(storageDir, $"{safeInstance}.dcm");
            await request.File.SaveAsync(dcmPath);

            // ── Convert pixel data to PNG (Cache) ──
            string? pngPath = null;

            if (rows.HasValue && columns.HasValue)
            {
                try
                {
                    var imageDir = Path.Combine(_imagesPath, safePatient, safeStudy, safeSeries);
                    Directory.CreateDirectory(imageDir);
                    pngPath = Path.Combine(imageDir, $"{safeInstance}.png");

                    // Use fo-dicom native image rendering
                    // This automatically handles JPEG decompression, 16-bit windowing, and photometric interpretation
                    var foDicomImage = new FellowOakDicom.Imaging.DicomImage(dataset);
                    // Update frameCount if fo-dicom finds more
                    frameCount = foDicomImage.NumberOfFrames;
                    
                    for (int i = 0; i < frameCount; i++)
                    {
                        var framePath = frameCount > 1 
                            ? pngPath.Replace(".png", $"_f{i}.png")
                            : pngPath;

                        using var bitmap = foDicomImage.RenderImage(i).As<System.Drawing.Bitmap>();
                        bitmap.Save(framePath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    _logger.LogInformation("Extracted {FrameCount} frames for SOP {SopUid}", frameCount, sopUid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not convert pixel data for SOP {SopUid}", sopUid);
                }
            }

            // ── Save DicomImage record ──
            await _dbLock.WaitAsync();
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
                await db.SaveChangesAsync();

                // ── Update study receiving state ──
                study.LastImageReceivedAt = DateTime.UtcNow;
                study.ImageCount = await db.Images
                    .CountAsync(i => db.Series.Where(s => s.StudyId == study.Id)
                    .Select(s => s.Id).Contains(i.SeriesId));
                study.Status = StudyStatus.Receiving;
                await db.SaveChangesAsync();
            }
            finally
            {
                _dbLock.Release();
            }

            _logger.LogInformation("""
                ✅ DICOM Received & Stored:
                ──────────────────────────────────────────────────────────
                Patient:    {PatientName} ({Sex}) [ID: {PatientId}]
                Study:      {StudyDesc} ({Modality})
                Date:       {StudyDate}
                Physician:  {Physician}
                Hospital:   {Institution}
                Frames:     {FrameCount}
                File:       {SafeInstance}.dcm
                ──────────────────────────────────────────────────────────
                """, 
                patientName, patientSex, patientIdTag,
                studyDesc, modality, 
                studyDate?.ToString("yyyy-MM-dd") ?? "N/A",
                referringPhysician, institution,
                frameCount, safeInstance);

            return new DicomCStoreResponse(request, DicomStatus.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process C-STORE request.");
            return new DicomCStoreResponse(request, DicomStatus.ProcessingFailure);
        }
    }

    public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
    {
        _logger.LogError(e, "C-STORE exception for temp file: {TempFile}", tempFileName);
        return Task.CompletedTask;
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

    private string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Unknown";
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized.Trim();
    }
}
