using FocusMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Data.Services;

/// <summary>
/// EF Core implementation of <see cref="IDocumentRepository"/>.
/// </summary>
public class DocumentRepository : IDocumentRepository
{
    private readonly FocusMedDbContext _db;

    public DocumentRepository(FocusMedDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status)
    {
        return await _db.Documents
            .AsNoTracking()
            .Where(d => d.Status == status)
            .OrderBy(d => d.ReceivedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Document>> GetRecentAsync(int count = 20)
    {
        return await _db.Documents
            .AsNoTracking()
            .OrderByDescending(d => d.ReceivedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        return await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Document> AddAsync(Document document)
    {
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();
        return document;
    }

    public async Task UpdateAsync(Document document)
    {
        _db.Documents.Update(document);
        await _db.SaveChangesAsync();
    }
}
