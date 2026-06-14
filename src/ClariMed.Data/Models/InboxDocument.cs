using System;

namespace ClariMed.Data.Models;

public class InboxDocument
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string PdfPath { get; set; } = string.Empty;
    public InboxDocumentStatus Status { get; set; } = InboxDocumentStatus.Unassigned;
    public int? AssignedToStudyId { get; set; }
    public Study? AssignedToStudy { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedAt { get; set; }
}

public enum InboxDocumentStatus
{
    Unassigned,
    Assigned
}
