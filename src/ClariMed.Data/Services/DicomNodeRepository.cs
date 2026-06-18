using ClariMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ClariMed.Data.Services;

public class DicomNodeRepository : IDicomNodeRepository
{
    private readonly ClariMedDbContext _db;

    public DicomNodeRepository(ClariMedDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DicomNode>> GetAllAsync()
    {
        return await _db.DicomNodes
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<DicomNode?> GetByIdAsync(int id)
    {
        return await _db.DicomNodes.FindAsync(id);
    }

    public async Task<DicomNode> AddAsync(DicomNode node)
    {
        _db.DicomNodes.Add(node);
        await _db.SaveChangesAsync();
        return node;
    }

    public async Task UpdateAsync(DicomNode node)
    {
        _db.DicomNodes.Update(node);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var node = await _db.DicomNodes.FindAsync(id);
        if (node != null)
        {
            _db.DicomNodes.Remove(node);
            await _db.SaveChangesAsync();
        }
    }
}
