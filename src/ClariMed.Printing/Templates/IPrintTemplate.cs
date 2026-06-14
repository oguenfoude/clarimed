using System.Drawing;

namespace ClariMed.Printing.Templates;

/// <summary>
/// Defines how a medical report page is rendered for printing.
/// </summary>
public interface IPrintTemplate
{
    /// <summary>
    /// Renders a complete print page onto the provided Graphics surface.
    /// </summary>
    /// <param name="graphics">GDI+ graphics surface from PrintDocument.</param>
    /// <param name="bounds">Printable area rectangle.</param>
    /// <param name="clinicName">Name of the clinic (header).</param>
    /// <param name="patientName">Patient full name.</param>
    /// <param name="patientId">Patient ID.</param>
    /// <param name="studyDescription">Study description text.</param>
    /// <param name="modality">Imaging modality (CR, CT, MR, etc.).</param>
    /// <param name="studyDate">Date the study was performed.</param>
    /// <param name="imagePath">Optional path to the converted image file to render on the page.</param>
    void Render(
        Graphics graphics,
        Rectangle bounds,
        string clinicName,
        string patientName,
        string patientId,
        string studyDescription,
        string modality,
        DateTime? studyDate,
        string? institutionName,
        string? referringPhysician,
        string? imagePath);
}
