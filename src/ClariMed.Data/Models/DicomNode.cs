namespace ClariMed.Data.Models;

public class DicomNode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AETitle { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 104;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
