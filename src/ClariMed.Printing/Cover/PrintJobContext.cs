namespace ClariMed.Printing.Cover;

/// <summary>
/// Immutable context object carrying all metadata needed to generate
/// a cover page ("Page de Garde") for a print job.
/// </summary>
public record PrintJobContext(
    string ClinicName,
    string PatientName,
    string PatientId,
    string StudyDescription,
    string Modality,
    DateTime? StudyDate,
    string? ReferringPhysician,
    string? InstitutionName,
    string? PatientSex = null,
    DateTime? PatientBirthDate = null,
    string? AccessionNumber = null);
