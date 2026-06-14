namespace ClariMed.Data.Models;

/// <summary>
/// Tracks the status of a received document through the conversion pipeline.
/// </summary>
public enum DocumentStatus
{
    /// <summary>File received, awaiting conversion.</summary>
    Pending,

    /// <summary>Conversion to PDF is in progress.</summary>
    Converting,

    /// <summary>Successfully converted to PDF.</summary>
    Converted,

    /// <summary>Conversion failed — see ErrorMessage for details.</summary>
    Failed
}

/// <summary>
/// Represents a medical report document (.docx) that has been received from any
/// ingestion source (watch folder, API upload, direct stream) and optionally
/// converted to PDF for merging into a final print job.
/// </summary>
public class Document
{
    public int Id { get; set; }

    /// <summary>Original file name as received (e.g., "Report_Patient123.docx").</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the stored .docx file on disk.
    /// Empty if the document was received as an in-memory stream and not yet persisted.
    /// </summary>
    public string OriginalFilePath { get; set; } = string.Empty;

    /// <summary>Absolute path to the converted PDF. Null until conversion succeeds.</summary>
    public string? PdfFilePath { get; set; }

    /// <summary>Current processing status of this document.</summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    /// <summary>UTC timestamp when the document was first received.</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when PDF conversion completed. Null if not yet converted.</summary>
    public DateTime? ConvertedAt { get; set; }

    /// <summary>Error details if conversion failed.</summary>
    public string? ErrorMessage { get; set; }

    // ── Navigation ──

    /// <summary>Optional FK — links this document to a specific DICOM Study.</summary>
    public int? StudyId { get; set; }
    public Study? Study { get; set; }
}
