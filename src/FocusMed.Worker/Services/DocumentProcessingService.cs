using FocusMed.Data;
using FocusMed.Data.Models;
using FocusMed.Documents.Converters;
using FocusMed.Documents.Ingestion;
using FocusMed.Data.Services;

namespace FocusMed.Worker.Services;

/// <summary>
/// Background consumer that drains the <see cref="DocumentIngestionQueue"/> channel.
/// For each incoming document:
///   1. Saves the file to disk (if received as a stream).
///   2. Calls <see cref="IDocumentConverter"/> to convert .docx → .pdf headlessly.
///   3. Creates a <see cref="Document"/> record in the database.
///
/// Error handling: If conversion fails for one document, the error is logged,
/// the Document record is marked as Failed, and the loop continues.
/// </summary>
public class DocumentProcessingService : BackgroundService
{
    private readonly DocumentIngestionQueue _queue;
    private readonly IDocumentConverter _converter;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        DocumentIngestionQueue queue,
        IDocumentConverter converter,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DocumentProcessingService> logger)
    {
        _queue = queue;
        _converter = converter;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentProcessingService started — waiting for incoming documents.");

        await foreach (var incoming in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessDocumentAsync(incoming, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown — stop processing
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document: {Name}", incoming.FileName);
                // The loop continues — one failure does not crash the service
            }
            finally
            {
                // Dispose the stream if it was an API upload
                incoming.ContentStream?.Dispose();
            }
        }

        _logger.LogInformation("DocumentProcessingService stopped.");
    }

    private async Task ProcessDocumentAsync(IncomingDocument incoming, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

        var outputDir = _configuration["FocusMed:DocumentOutputPath"] ?? "data/Documents";
        Directory.CreateDirectory(outputDir);

        // Determine the source file path
        string docxPath;
        if (!string.IsNullOrEmpty(incoming.SourcePath) && File.Exists(incoming.SourcePath))
        {
            // File from WatchFolder — use directly
            docxPath = incoming.SourcePath;
        }
        else if (incoming.ContentStream != null)
        {
            // Stream from API — save to a temp file first
            var tempDir = Path.Combine(outputDir, "temp");
            Directory.CreateDirectory(tempDir);
            docxPath = Path.Combine(tempDir, incoming.FileName);

            await using var fileStream = File.Create(docxPath);
            await incoming.ContentStream.CopyToAsync(fileStream, ct);
        }
        else
        {
            _logger.LogWarning("Document {Name} has no source path or stream — skipping.", incoming.FileName);
            return;
        }

        // Create the Document record
        var document = new Document
        {
            OriginalFileName = incoming.FileName,
            OriginalFilePath = docxPath,
            Status = DocumentStatus.Converting,
            ReceivedAt = DateTime.UtcNow
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        try
        {
            string pdfPath;
            if (Path.GetExtension(docxPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // File is already a PDF (e.g. from Virtual Printer)
                var newPdfPath = Path.Combine(outputDir, incoming.FileName);
                if (!docxPath.Equals(newPdfPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(docxPath, newPdfPath, true);
                }
                pdfPath = newPdfPath;
                _logger.LogInformation("Document is already PDF, copied to: {Pdf}", pdfPath);
            }
            else
            {
                // Convert .docx → .pdf
                pdfPath = await _converter.ConvertDocxToPdfAsync(docxPath, outputDir, ct);
                _logger.LogInformation("Document converted: {Name} → {Pdf}", incoming.FileName, pdfPath);
            }

            document.PdfFilePath = pdfPath;
            document.Status = DocumentStatus.Converted;
            document.ConvertedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // If the file came from WatchFolder, delete the original so the next print is a new Create event
            if (!string.IsNullOrEmpty(incoming.SourcePath) && File.Exists(incoming.SourcePath))
            {
                try { File.Delete(incoming.SourcePath); } catch { /* Ignore locked file */ }
            }
        }
        catch (Exception ex)
        {
            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(ct);

            _logger.LogError(ex, "Document conversion failed: {Name}", incoming.FileName);
        }
    }
}
