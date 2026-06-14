namespace ClariMed.Data.Models;

public class DicomImage
{
    public int Id { get; set; }
    public string SopInstanceUid { get; set; } = string.Empty;
    public int SeriesId { get; set; }
    public int? InstanceNumber { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int? Rows { get; set; }
    public int? Columns { get; set; }

    /// <summary>Number of frames extracted from this DICOM file (1 for single-frame, N for multi-frame/cine).</summary>
    public int FrameCount { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Series Series { get; set; } = null!;
}
