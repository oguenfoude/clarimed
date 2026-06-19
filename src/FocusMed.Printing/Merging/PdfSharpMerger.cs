using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace FocusMed.Printing.Merging;

/// <summary>
/// Merges PDFs and PNG images into a single PDF using PdfSharpCore.
/// 
/// Memory safety: every PdfDocument, XGraphics, and XImage is wrapped
/// in a using block. No intermediate object survives beyond its page render.
/// </summary>
public class PdfSharpMerger : IPdfMerger
{
    private readonly ILogger<PdfSharpMerger> _logger;

    // A4 dimensions in points (72 dpi)
    private const double A4WidthPt = 595.28;
    private const double A4HeightPt = 841.89;

    public PdfSharpMerger(ILogger<PdfSharpMerger> logger)
    {
        _logger = logger;
    }

    public Task<string> MergeAsync(
        string coverPdfPath,
        string? reportPdfPath,
        IEnumerable<string> additionalPdfPaths,
        IEnumerable<string> imagePngPaths,
        string outputPath,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var output = new PdfDocument();

            // 1. Append cover page(s)
            AppendPdfPages(output, coverPdfPath);
            _logger.LogDebug("Appended cover page.");

            // 2. Append report PDF pages (if present)
            if (!string.IsNullOrWhiteSpace(reportPdfPath) && File.Exists(reportPdfPath))
            {
                AppendPdfPages(output, reportPdfPath);
                _logger.LogDebug("Appended report PDF.");
            }

            // 2b. Append additional PDF pages (if present)
            if (additionalPdfPaths != null)
            {
                foreach (var pdfPath in additionalPdfPaths)
                {
                    if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
                    {
                        AppendPdfPages(output, pdfPath);
                        _logger.LogDebug("Appended additional PDF: {Path}", pdfPath);
                    }
                }
            }

            // 3. Append each DICOM PNG as a new A4 page
            foreach (var pngPath in imagePngPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(pngPath))
                {
                    _logger.LogWarning("Image file not found, skipping: {Path}", pngPath);
                    continue;
                }

                AppendImageAsPage(output, pngPath);
            }

            // 4. Save the final merged PDF
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            output.Save(outputPath);

            _logger.LogInformation("Merged PDF created ({Pages} pages): {Path}",
                output.PageCount, outputPath);

            return outputPath;

        }, cancellationToken);
    }

    /// <summary>
    /// Imports all pages from an existing PDF into the output document.
    /// The source document is fully disposed after copying.
    /// </summary>
    private static void AppendPdfPages(PdfDocument target, string sourcePath)
    {
        using var source = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
        foreach (var page in source.Pages)
        {
            target.AddPage(page);
        }
        // source disposed here — file handle released
    }

    /// <summary>
    /// Creates a new A4 page and draws the PNG image scaled to fit,
    /// preserving aspect ratio and centering on the page.
    /// The XImage and XGraphics are disposed immediately after rendering.
    /// </summary>
    private static void AppendImageAsPage(PdfDocument target, string imagePath)
    {
        var page = target.AddPage();
        page.Width = A4WidthPt;
        page.Height = A4HeightPt;

        // Margins (20pt each side)
        const double margin = 20;
        double drawableWidth = page.Width - (2 * margin);
        double drawableHeight = page.Height - (2 * margin);

        using var gfx = XGraphics.FromPdfPage(page);
        using var image = XImage.FromFile(imagePath);

        // Scale to fit within the drawable area, preserving aspect ratio
        double scaleX = drawableWidth / image.PixelWidth;
        double scaleY = drawableHeight / image.PixelHeight;
        double scale = Math.Min(scaleX, scaleY);

        double scaledWidth = image.PixelWidth * scale;
        double scaledHeight = image.PixelHeight * scale;

        // Center the image on the page
        double x = margin + (drawableWidth - scaledWidth) / 2;
        double y = margin + (drawableHeight - scaledHeight) / 2;

        gfx.DrawImage(image, x, y, scaledWidth, scaledHeight);
        // image + gfx disposed here — no memory leak
    }
}
