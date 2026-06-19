namespace FocusMed.Printing.Engines;

/// <summary>
/// Sends rendered medical reports to a Windows printer.
/// </summary>
public interface IPrintEngine
{
    /// <summary>
    /// Prints a medical report page to the specified printer.
    /// </summary>
    /// <param name="printerName">Windows printer name (empty = default printer).</param>
    /// <param name="clinicName">Clinic name for the header.</param>
    /// <param name="patientName">Patient full name.</param>
    /// <param name="patientId">Patient ID.</param>
    /// <param name="studyDescription">Study description.</param>
    /// <param name="modality">Imaging modality.</param>
    /// <param name="studyDate">Date the study was performed.</param>
    /// <param name="imagePath">Optional path to the image file to print.</param>
    /// <returns>True if printing succeeded, false otherwise.</returns>
    Task<bool> PrintAsync(
        string printerName,
        string clinicName,
        string patientName,
        string patientId,
        string studyDescription,
        string modality,
        DateTime? studyDate,
        string? institutionName,
        string? referringPhysician,
        string? imagePath);

    /// <summary>
    /// Returns a list of available Windows printer names.
    /// </summary>
    IReadOnlyList<string> GetAvailablePrinters();
}
