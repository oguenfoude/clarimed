using Microsoft.Extensions.Logging;
using Spire.Doc;

namespace FocusMed.Documents.Converters;

/// <summary>
/// Headless .docx → .pdf converter using FreeSpire.Doc.
/// No Microsoft Word installation required. No UI. Fully in-memory.
/// </summary>
public class SpireDocumentConverter : IDocumentConverter
{
    private readonly ILogger<SpireDocumentConverter> _logger;

    public SpireDocumentConverter(ILogger<SpireDocumentConverter> logger)
    {
        _logger = logger;
    }

    public async Task<string> ConvertDocxToPdfAsync(
        string docxPath, string outputDir, CancellationToken cancellationToken)
    {
        if (!File.Exists(docxPath))
            throw new FileNotFoundException("Source .docx file not found.", docxPath);

        var outputFileName = Path.GetFileNameWithoutExtension(docxPath);

        return await Task.Run(() =>
        {
            var document = new Document();
            document.LoadFromFile(docxPath);
            Directory.CreateDirectory(outputDir);
            var pdfPath = Path.Combine(outputDir, outputFileName + ".pdf");
            document.SaveToFile(pdfPath, FileFormat.PDF);
            _logger.LogInformation("Converted document to PDF: {Path}", pdfPath);
            return pdfPath;
        }, cancellationToken);
    }

    public async Task<string> ConvertDocxToPdfAsync(
        Stream docxStream, string outputFileName, string outputDir, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var document = new Document();
            // FreeSpire.Doc expects format type on load from stream. Since the files from watch folder are .docx:
            document.LoadFromStream(docxStream, FileFormat.Docx);
            Directory.CreateDirectory(outputDir);
            var pdfPath = Path.Combine(outputDir, outputFileName + ".pdf");
            document.SaveToFile(pdfPath, FileFormat.PDF);
            _logger.LogInformation("Converted document from stream to PDF: {Path}", pdfPath);
            return pdfPath;
        }, cancellationToken);
    }
}
