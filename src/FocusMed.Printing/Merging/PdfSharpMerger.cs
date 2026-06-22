using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace FocusMed.Printing.Merging;

/// <summary>
/// Merges PDFs and PNG images into a single PDF using PdfSharpCore.
/// Images are laid out in a grid on A4 pages, then optionally imposed for A3 or booklet.
/// </summary>
public class PdfSharpMerger : IPdfMerger
{
    private readonly ILogger<PdfSharpMerger> _logger;

    private const double A4WidthPt = 595.28;
    private const double A4HeightPt = 841.89;
    private static readonly double A3WidthPt = A4WidthPt * Math.Sqrt(2);
    private static readonly double A3HeightPt = A4HeightPt * Math.Sqrt(2);

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
        PrintFormat format,
        int imagesPerPage,
        int columnsPerRow,
        int gapPx,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var output = new PdfDocument();

            // 1. Append cover page(s)
            if (!string.IsNullOrWhiteSpace(coverPdfPath) && File.Exists(coverPdfPath))
            {
                AppendPdfPages(output, coverPdfPath);
                _logger.LogDebug("Appended cover page.");
            }

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

            // 3. Append DICOM images as grid-layout A4 pages
            var validImages = imagePngPaths
                .Where(p => File.Exists(p))
                .ToList();

            int skipped = imagePngPaths.Count() - validImages.Count;
            if (skipped > 0)
                _logger.LogWarning("Skipped {Count} missing image files.", skipped);

            AppendImageGridPages(output, validImages, imagesPerPage, columnsPerRow, gapPx);
            _logger.LogInformation("Laid out {Count} images into grid pages ({PerPage}/page, {Cols} cols, {Gap}px gap).",
                validImages.Count, imagesPerPage, columnsPerRow, gapPx);

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            // 4. Format-specific output
            if (format == PrintFormat.A4)
            {
                output.Save(outputPath);
                _logger.LogInformation("A4 PDF created ({Pages} pages): {Path}", output.PageCount, outputPath);
                return outputPath;
            }

            if (format == PrintFormat.A3Standard)
            {
                var a3Path = ConvertA4ToA3(output);
                File.Move(a3Path, outputPath, overwrite: true);
                _logger.LogInformation("A3 Standard PDF created: {Path}", outputPath);
                return outputPath;
            }

            // A3Booklet
            var bookletPath = CreateBooklet(output);
            File.Move(bookletPath, outputPath, overwrite: true);
            _logger.LogInformation("Booklet PDF created: {Path}", outputPath);
            return outputPath;

        }, cancellationToken);
    }

    /// <summary>
    /// Creates A4 pages with images arranged in a grid layout.
    /// Each page has (imagesPerPage) images arranged in (columnsPerRow) columns.
    /// </summary>
    private static void AppendImageGridPages(PdfDocument target, List<string> imagePaths, int imagesPerPage, int columnsPerRow, int gapPx)
    {
        if (imagePaths.Count == 0) return;

        if (imagesPerPage <= 0) imagesPerPage = 1;
        if (columnsPerRow <= 0) columnsPerRow = 1;
        double gap = Math.Max(0, gapPx);

        // PDF margin = 2 * gapPx per architectural rules
        double margin = 2 * gap;
        double usableWidth = A4WidthPt - (2 * margin);
        double usableHeight = A4HeightPt - (2 * margin);

        for (int pageStart = 0; pageStart < imagePaths.Count; pageStart += imagesPerPage)
        {
            var page = target.AddPage();
            page.Width = A4WidthPt;
            page.Height = A4HeightPt;

            using var gfx = XGraphics.FromPdfPage(page);

            int count = Math.Min(imagesPerPage, imagePaths.Count - pageStart);
            int actualRows = (int)Math.Ceiling(count / (double)columnsPerRow);

            // Step 1: Layout as if width is fully utilized (mimicking HTML grid behavior)
            double cellWidth = (usableWidth - gap * (columnsPerRow - 1)) / columnsPerRow;
            double[] rowHeights = new double[actualRows];

            // Load images and calculate intrinsic row heights
            var pageImages = new XImage[count];
            for (int idx = 0; idx < count; idx++)
            {
                var imagePath = imagePaths[pageStart + idx];
                var img = XImage.FromFile(imagePath);
                pageImages[idx] = img;

                int row = idx / columnsPerRow;
                double scaledH = img.PixelHeight * (cellWidth / img.PixelWidth);
                if (scaledH > rowHeights[row])
                {
                    rowHeights[row] = scaledH;
                }
            }

            double totalGridHeight = rowHeights.Sum() + gap * (actualRows - 1);

            // Step 2: Scale down the entire grid if it exceeds A4 page height
            double gridScale = 1.0;
            if (totalGridHeight > usableHeight && totalGridHeight > 0)
            {
                gridScale = usableHeight / totalGridHeight;
            }

            double finalCellWidth = cellWidth * gridScale;
            double finalGap = gap * gridScale;
            for (int r = 0; r < actualRows; r++) rowHeights[r] *= gridScale;

            // Center horizontally if scaled down
            double offsetX = margin + (usableWidth - (usableWidth * gridScale)) / 2;
            double currentY = margin;

            // Step 3: Draw images
            for (int r = 0; r < actualRows; r++)
            {
                for (int c = 0; c < columnsPerRow; c++)
                {
                    int idx = r * columnsPerRow + c;
                    if (idx >= count) break;

                    var img = pageImages[idx];
                    
                    double drawX = offsetX + c * (finalCellWidth + finalGap);
                    double drawY = currentY;
                    
                    // w-full h-auto equivalent
                    double finalImgW = finalCellWidth;
                    double finalImgH = img.PixelHeight * (finalCellWidth / img.PixelWidth);

                    gfx.DrawImage(img, drawX, drawY, finalImgW, finalImgH);
                }
                
                currentY += rowHeights[r] + finalGap;
            }

            // Cleanup
            foreach (var img in pageImages)
            {
                img.Dispose();
            }
        }
    }

    /// <summary>
    /// Converts A4 pages to A3 portrait by scaling each page to fit A3.
    /// </summary>
    private string ConvertA4ToA3(PdfDocument a4Doc)
    {
        using var stream = new MemoryStream();
        a4Doc.Save(stream, false);
        stream.Position = 0;

        using var a3Output = new PdfDocument();
        using var form = XPdfForm.FromStream(stream);

        for (int i = 0; i < a4Doc.PageCount; i++)
        {
            var a3Page = a3Output.AddPage();
            a3Page.Width = A3WidthPt;
            a3Page.Height = A3HeightPt;

            using var gfx = XGraphics.FromPdfPage(a3Page);
            form.PageNumber = i + 1;

            // Scale A4 content to fill A3
            double scaleX = A3WidthPt / A4WidthPt;
            double scaleY = A3HeightPt / A4HeightPt;
            double scale = Math.Min(scaleX, scaleY);

            double drawW = A4WidthPt * scale;
            double drawH = A4HeightPt * scale;
            double offsetX = (A3WidthPt - drawW) / 2;
            double offsetY = (A3HeightPt - drawH) / 2;

            gfx.DrawImage(form, offsetX, offsetY, drawW, drawH);
        }

        var tmpPath = Path.Combine(Path.GetTempPath(), $"a3_{Guid.NewGuid()}.pdf");
        a3Output.Save(tmpPath);
        return tmpPath;
    }

    /// <summary>
    /// Creates booklet imposition: A4 pages paired side-by-side on A3 landscape sheets.
    /// Front: [last-i, i] | Back: [i+1, last-1-i]
    /// </summary>
    private string CreateBooklet(PdfDocument a4Doc)
    {
        // Pad to multiple of 4
        while (a4Doc.PageCount % 4 != 0)
        {
            var blank = a4Doc.AddPage();
            blank.Width = A4WidthPt;
            blank.Height = A4HeightPt;
        }

        int totalPages = a4Doc.PageCount;
        int sheets = totalPages / 4;
        _logger.LogInformation("Booklet: {Pages} A4 pages → {Sheets} A3 sheets", totalPages, sheets);

        using var bStream = new MemoryStream();
        a4Doc.Save(bStream, false);
        bStream.Position = 0;

        using var booklet = new PdfDocument();
        using var bForm = XPdfForm.FromStream(bStream);

        // A3 landscape = two A4 pages side by side
        double a3LandscapeWidth = A4WidthPt * 2;   // 1190.56pt
        double a3LandscapeHeight = A4HeightPt;      // 841.89pt

        for (int i = 0; i < sheets; i++)
        {
            // Front side
            var frontPage = booklet.AddPage();
            frontPage.Width = a3LandscapeWidth;
            frontPage.Height = a3LandscapeHeight;

            using (var gfxFront = XGraphics.FromPdfPage(frontPage))
            {
                bForm.PageNumber = totalPages - 2 * i;
                gfxFront.DrawImage(bForm, 0, 0, A4WidthPt, A4HeightPt);

                bForm.PageNumber = 1 + 2 * i;
                gfxFront.DrawImage(bForm, A4WidthPt, 0, A4WidthPt, A4HeightPt);
            }

            // Back side
            var backPage = booklet.AddPage();
            backPage.Width = a3LandscapeWidth;
            backPage.Height = a3LandscapeHeight;

            using (var gfxBack = XGraphics.FromPdfPage(backPage))
            {
                bForm.PageNumber = 2 + 2 * i;
                gfxBack.DrawImage(bForm, 0, 0, A4WidthPt, A4HeightPt);

                bForm.PageNumber = totalPages - 1 - 2 * i;
                gfxBack.DrawImage(bForm, A4WidthPt, 0, A4WidthPt, A4HeightPt);
            }
        }

        var tmpPath = Path.Combine(Path.GetTempPath(), $"booklet_{Guid.NewGuid()}.pdf");
        booklet.Save(tmpPath);
        return tmpPath;
    }

    private static void AppendPdfPages(PdfDocument target, string sourcePath)
    {
        using var source = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
        foreach (var page in source.Pages)
        {
            target.AddPage(page);
        }
    }
}
