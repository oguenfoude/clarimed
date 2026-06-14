namespace ClariMed.Data.Models;

public class Study
{
    public int Id { get; set; }
    public string StudyInstanceUid { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public string AccessionNumber { get; set; } = string.Empty;
    public DateTime? StudyDate { get; set; }
    public string StudyDescription { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string ReferringPhysicianName { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Current lifecycle state — Receiving or Complete.</summary>
    public StudyStatus Status { get; set; } = StudyStatus.Receiving;

    /// <summary>UTC timestamp of the most recent DICOM file arrival (stabilization anchor).</summary>
    public DateTime LastImageReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Total number of DicomImage records under this study (across all series).</summary>
    public int ImageCount { get; set; }

    /// <summary>UTC timestamp when the study was marked complete. Null while receiving.</summary>
    public DateTime? CompletedAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public List<Series> SeriesList { get; set; } = new();
}
