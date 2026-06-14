namespace ClariMed.Documents.Converters;

/// <summary>
/// Converts document files (.docx) to PDF format.
/// Implementations must be thread-safe and perform headless conversion
/// (no UI, no Word installation required).
/// </summary>
public interface IDocumentConverter
{
    /// <summary>
    /// Converts a .docx file to PDF.
    /// </summary>
    /// <param name="docxPath">Absolute path to the source .docx file.</param>
    /// <param name="outputDir">Directory where the output .pdf will be saved.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the generated .pdf file.</returns>
    Task<string> ConvertDocxToPdfAsync(string docxPath, string outputDir, CancellationToken cancellationToken);

    /// <summary>
    /// Converts a .docx stream to PDF.
    /// </summary>
    /// <param name="docxStream">Stream containing .docx content.</param>
    /// <param name="outputFileName">Desired output file name (without extension).</param>
    /// <param name="outputDir">Directory where the output .pdf will be saved.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the generated .pdf file.</returns>
    Task<string> ConvertDocxToPdfAsync(Stream docxStream, string outputFileName, string outputDir, CancellationToken cancellationToken);
}
