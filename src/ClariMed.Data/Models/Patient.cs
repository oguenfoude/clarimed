namespace ClariMed.Data.Models;

public class Patient
{
    public int Id { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string Sex { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Study> Studies { get; set; } = new();
}
