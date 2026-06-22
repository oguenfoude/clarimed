using System.Text;
using FocusMed.Data;
using FocusMed.Data.Models;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Dicom.Handlers;

/// <summary>
/// DICOM C-STORE SCP handler. Receives DICOM images from modalities and:
///   1. Saves the raw .dcm file to disk
///   2. Extracts DICOM tags and persists Patient → Study → Series → DicomImage
///   3. Converts pixel data to PNG
/// </summary>
public class CStoreScp : DicomService, IDicomServiceProvider, IDicomCStoreProvider
{
    private static readonly DicomTransferSyntax[] AcceptedTransferSyntaxes = new[]
    {
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.ExplicitVRBigEndian,
        DicomTransferSyntax.ImplicitVRLittleEndian,
        DicomTransferSyntax.JPEGProcess1,
        DicomTransferSyntax.JPEGProcess2_4,
        DicomTransferSyntax.JPEGProcess14,
        DicomTransferSyntax.JPEGProcess14SV1,
        DicomTransferSyntax.JPEG2000Lossless,
        DicomTransferSyntax.JPEG2000Lossy,
        DicomTransferSyntax.RLELossless,
    };

    private readonly IServiceProvider _rootProvider;
    private readonly ILogger<CStoreScp> _logger;
    private readonly string _archivePath;
    private readonly string _imagesPath;

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
        _archivePath = config["FocusMed:ArchivePath"] ?? "data/archive";
        _archivePath = Path.GetFullPath(_archivePath);
        var rootDataPath = Path.GetDirectoryName(_archivePath) ?? "data";
        _imagesPath = config["FocusMed:ImagesPath"] ?? Path.Combine(rootDataPath, "images");
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
            using var scope = _rootProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

            var dataset = request.Dataset;
            var tags = Services.DicomUpsertService.ExtractTags(dataset);

            var studyLock = Services.DicomUpsertService.GetStudyLock(tags.StudyUid);
            await studyLock.WaitAsync();
            try
            {
                // Batched upsert: single SaveChangesAsync for Patient + Study + Series
                var (patient, study, series) = await Services.DicomUpsertService.UpsertEntitiesAsync(db, tags);

                // Check duplicate inside the lock
                if (Services.DicomUpsertService.IsDuplicate(db, tags.SopUid))
                {
                    _logger.LogInformation("Duplicate SOP Instance UID {SopUid} — already stored, skipping.", tags.SopUid);
                    return new DicomCStoreResponse(request, DicomStatus.Success);
                }

                // Generate paths
                var pathParts = Services.DicomUpsertService.GeneratePaths(tags);

                // Save .dcm file
                var dcmPath = Path.Combine(_archivePath, pathParts[0], pathParts[1], pathParts[2], $"{pathParts[3]}.dcm");
                var dir = Path.GetDirectoryName(dcmPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                await request.File.SaveAsync(dcmPath);

                // Convert to PNG
                var pngPaths = Services.DicomUpsertService.ConvertToPng(dataset, "", tags.Rows, tags.Columns, _imagesPath, pathParts, _logger);

                // Save image record + update study count — single SaveChangesAsync
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
                    .Select(s => s.Id).Contains(i.SeriesId));
                study.Status = StudyStatus.Receiving;

                await db.SaveChangesAsync();

                _logger.LogInformation("Image received: {PatientName} / {Modality} / {SopUid}", tags.PatientName, tags.Modality, tags.SopUid);
            }
            finally
            {
                studyLock.Release();
            }

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
}
