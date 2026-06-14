namespace ClariMed.Data.Models;

/// <summary>
/// Tracks the lifecycle of a DICOM study as files arrive from modalities.
/// Used for dashboard visibility — NOT connected to print triggering.
/// </summary>
public enum StudyStatus
{
    /// <summary>Files are still arriving from the modality.</summary>
    Receiving,

    /// <summary>Stabilization window passed — all files received.</summary>
    Complete
}
