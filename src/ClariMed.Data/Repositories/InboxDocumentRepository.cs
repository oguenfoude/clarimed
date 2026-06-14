using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClariMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ClariMed.Data.Repositories;

public interface IInboxDocumentRepository
{
    Task<List<InboxDocument>> GetUnassignedAsync();
    Task<InboxDocument> AddAsync(InboxDocument doc);
    Task AssignToStudyAsync(int inboxDocId, int studyId);
}

public class InboxDocumentRepository : IInboxDocumentRepository
{
    private readonly ClariMedDbContext _db;

    public InboxDocumentRepository(ClariMedDbContext db)
    {
        _db = db;
    }

    public async Task<List<InboxDocument>> GetUnassignedAsync()
    {
        return await _db.InboxDocuments
            .Where(d => d.Status == InboxDocumentStatus.Unassigned)
            .OrderByDescending(d => d.ReceivedAt)
            .ToListAsync();
    }

    public async Task<InboxDocument> AddAsync(InboxDocument doc)
    {
        _db.InboxDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task AssignToStudyAsync(int inboxDocId, int studyId)
    {
        var doc = await _db.InboxDocuments.FindAsync(inboxDocId);
        if (doc != null && doc.Status == InboxDocumentStatus.Unassigned)
        {
            doc.Status = InboxDocumentStatus.Assigned;
            doc.AssignedToStudyId = studyId;
            doc.AssignedAt = System.DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
