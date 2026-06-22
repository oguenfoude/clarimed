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
/// Core DICOM Service Class Provider (SCP) for FocusMed.
/// This handler manages three distinct DICOM workflows:
///   1. C-ECHO (Ping): Verifies connectivity with modalities.
///   2. C-STORE (Images): Receives standard DICOM images, saves them to disk, converts to PNG, and stores them in the database.
///   3. N-SERVICES (DICOM Print): Intercepts DICOM print jobs (virtual printer) and ingests the rendered films as standard studies.
/// </summary>
public class DicomScp : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomNServiceProvider, IDicomCEchoProvider
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
    private readonly ILogger<DicomScp> _logger;
    private readonly string _archivePath;
    private readonly string _imagesPath;

    public DicomScp(
        INetworkStream stream,
        Encoding fallbackEncoding,
        ILogger logger,
        DicomServiceDependencies dependencies,
        IServiceProvider serviceProvider)
        : base(stream, fallbackEncoding, logger, dependencies)
    {
        _rootProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<DicomScp>>();
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

                // Convert to PNG asynchronously to avoid blocking C-STORE response and causing modality timeouts
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Open from the saved file to ensure we don't read from a disposed network TempFileBuffer
                        var savedFile = await FellowOakDicom.DicomFile.OpenAsync(dcmPath);
                        Services.DicomUpsertService.ConvertToPng(savedFile.Dataset, "", tags.Rows, tags.Columns, _imagesPath, pathParts, _logger);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background PNG conversion failed for {SopUid}", tags.SopUid);
                    }
                });

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

    private string? _printStudyUid;
    private string? _printSeriesUid;
    private int _printInstanceCount = 0;

    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
    {
        _logger.LogInformation("C-ECHO received and answered successfully.");
        return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
    }

    public Task<DicomNEventReportResponse> OnNEventReportRequestAsync(DicomNEventReportRequest request)
    {
        return Task.FromResult(new DicomNEventReportResponse(request, DicomStatus.Success));
    }

    public Task<DicomNGetResponse> OnNGetRequestAsync(DicomNGetRequest request)
    {
        var response = new DicomNGetResponse(request, DicomStatus.Success);
        if (request.SOPClassUID == DicomUID.Printer)
        {
            var dataset = new DicomDataset();
            dataset.Add(DicomTag.PrinterStatus, "NORMAL");
            dataset.Add(DicomTag.PrinterName, "FocusMed Print SCP");
            dataset.Add(DicomTag.Manufacturer, "FocusMed");
            response.Dataset = dataset;
        }
        return Task.FromResult(response);
    }

    public Task<DicomNCreateResponse> OnNCreateRequestAsync(DicomNCreateRequest request)
    {
        return Task.FromResult(new DicomNCreateResponse(request, DicomStatus.Success));
    }

    public async Task<DicomNSetResponse> OnNSetRequestAsync(DicomNSetRequest request)
    {
        if (request.Dataset != null)
        {
            DicomSequence? seq = null;
            if (request.Dataset.Contains(DicomTag.BasicGrayscaleImageSequence))
                seq = request.Dataset.GetSequence(DicomTag.BasicGrayscaleImageSequence);
            else if (request.Dataset.Contains(DicomTag.BasicColorImageSequence))
                seq = request.Dataset.GetSequence(DicomTag.BasicColorImageSequence);

            if (seq != null && seq.Items.Count > 0)
            {
                var imageDataset = seq.Items[0];

                if (_printStudyUid == null)
                {
                    _printStudyUid = FellowOakDicom.DicomUIDGenerator.GenerateDerivedFromUUID().UID;
                    _printSeriesUid = FellowOakDicom.DicomUIDGenerator.GenerateDerivedFromUUID().UID;
                }
                _printInstanceCount++;

                var sopUid = FellowOakDicom.DicomUIDGenerator.GenerateDerivedFromUUID().UID;

                imageDataset.AddOrUpdate(DicomTag.PatientID, "PRINT-" + DateTime.Now.ToString("yyyyMMdd"));
                imageDataset.AddOrUpdate(DicomTag.PatientName, "Printed Film");
                imageDataset.AddOrUpdate(DicomTag.StudyInstanceUID, _printStudyUid);
                imageDataset.AddOrUpdate(DicomTag.SeriesInstanceUID, _printSeriesUid);
                imageDataset.AddOrUpdate(DicomTag.SOPInstanceUID, sopUid);
                imageDataset.AddOrUpdate(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage);
                imageDataset.AddOrUpdate(DicomTag.Modality, "PR");
                imageDataset.AddOrUpdate(DicomTag.StudyDescription, "DICOM Print Job");
                imageDataset.AddOrUpdate(DicomTag.SeriesDescription, "Rendered Films");
                imageDataset.AddOrUpdate(DicomTag.InstanceNumber, _printInstanceCount);
                imageDataset.AddOrUpdate(DicomTag.StudyDate, DateTime.Now.ToString("yyyyMMdd"));

                var file = new FellowOakDicom.DicomFile(imageDataset);
                var mockRequest = new DicomCStoreRequest(file);
                await OnCStoreRequestAsync(mockRequest);
            }
        }
        return new DicomNSetResponse(request, DicomStatus.Success);
    }

    public Task<DicomNActionResponse> OnNActionRequestAsync(DicomNActionRequest request)
    {
        return Task.FromResult(new DicomNActionResponse(request, DicomStatus.Success));
    }

    public Task<DicomNDeleteResponse> OnNDeleteRequestAsync(DicomNDeleteRequest request)
    {
        return Task.FromResult(new DicomNDeleteResponse(request, DicomStatus.Success));
    }
}
