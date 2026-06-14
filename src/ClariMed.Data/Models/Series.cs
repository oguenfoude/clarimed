namespace ClariMed.Data.Models;

public class Series
{
    public int Id { get; set; }
    public string SeriesInstanceUid { get; set; } = string.Empty;
    public int StudyId { get; set; }
    public int? SeriesNumber { get; set; }
    public string Modality { get; set; } = string.Empty;
    public string SeriesDescription { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Study Study { get; set; } = null!;
    public List<DicomImage> Images { get; set; } = new();
}
