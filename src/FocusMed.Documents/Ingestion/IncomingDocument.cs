namespace FocusMed.Documents.Ingestion;

/// <summary>
/// Immutable data object representing a document that has arrived
/// from any source (watch folder, API upload, direct stream).
/// </summary>
/// <param name="FileName">Original file name (e.g., "Report_Patient123.docx").</param>
/// <param name="SourcePath">
/// Absolute file path if the document originated from disk.
/// Empty string if it was received as an in-memory stream.
/// </param>
/// <param name="ContentStream">
/// In-memory content stream if the document was uploaded via API.
/// Null if the document is available as a file on disk.
/// The consumer is responsible for disposing this stream after use.
/// </param>
public record IncomingDocument(
    string FileName,
    string SourcePath,
    Stream? ContentStream);
