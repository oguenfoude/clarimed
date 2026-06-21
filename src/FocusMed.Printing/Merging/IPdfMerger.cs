namespace FocusMed.Printing.Merging;

public enum PrintFormat
{
    A4,
    A3Standard,
    A3Booklet
}

/// <summary>
/// Merges multiple PDF sources and image files into a single PDF document.
/// The merge order is: Cover Page → Report PDF → DICOM Image Pages.
/// </summary>
public interface IPdfMerger
{
    /// <summary>
    /// Merges a cover page PDF, an optional report PDF, and zero or more PNG images
    /// into a single output PDF file.
    /// </summary>
    Task<string> MergeAsync(
        string coverPdfPath,
        string? reportPdfPath,
        IEnumerable<string> additionalPdfPaths,
        IEnumerable<string> imagePngPaths,
        string outputPath,
        PrintFormat format,
        int imagesPerPage,
        int columnsPerRow,
        int gapPx,
        CancellationToken cancellationToken);
}
