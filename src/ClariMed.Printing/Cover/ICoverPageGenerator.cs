namespace ClariMed.Printing.Cover;

/// <summary>
/// Generates a PDF cover page ("Page de Garde") for a print job,
/// containing clinic branding, patient demographics, and study metadata.
/// </summary>
public interface ICoverPageGenerator
{
    /// <summary>
    /// Generates a single-page PDF cover page.
    /// </summary>
    /// <param name="context">Patient/study metadata for the cover page.</param>
    /// <param name="outputDir">Directory where the cover PDF will be saved.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the generated cover page PDF.</returns>
    Task<string> GenerateAsync(PrintJobContext context, string outputDir, CancellationToken cancellationToken);
}
