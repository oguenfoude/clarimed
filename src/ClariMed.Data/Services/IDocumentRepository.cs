using ClariMed.Data.Models;

namespace ClariMed.Data.Services;

/// <summary>
/// Data access interface for Document entities.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>Get all documents with a given status, ordered by ReceivedAt ascending.</summary>
    Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status);

    /// <summary>Get recent documents across all statuses.</summary>
    Task<IReadOnlyList<Document>> GetRecentAsync(int count = 20);

    /// <summary>Find a document by its primary key.</summary>
    Task<Document?> GetByIdAsync(int id);

    /// <summary>Add a new document record.</summary>
    Task<Document> AddAsync(Document document);

    /// <summary>Persist changes to an existing tracked document.</summary>
    Task UpdateAsync(Document document);
}
