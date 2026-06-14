namespace ClariMed.Printing.Merging;

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
    /// <param name="coverPdfPath">Path to the cover page PDF (required).</param>
    /// <param name="reportPdfPath">Path to the report PDF (optional, null if no report).</param>
    /// <param name="additionalPdfPaths">Paths to additional PDF files to append after the report.</param>
    /// <param name="imagePngPaths">Paths to DICOM PNG images to append as pages.</param>
    /// <param name="outputPath">Full path for the merged output PDF.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the merged PDF (same as outputPath).</returns>
    Task<string> MergeAsync(
        string coverPdfPath,
        string? reportPdfPath,
        IEnumerable<string> additionalPdfPaths,
        IEnumerable<string> imagePngPaths,
        string outputPath,
        CancellationToken cancellationToken);
}
