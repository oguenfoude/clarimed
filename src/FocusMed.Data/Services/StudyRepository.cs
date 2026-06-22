using FocusMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Data.Services;

public class StudyRepository : IStudyRepository
{
    private readonly FocusMedDbContext _db;

    public StudyRepository(FocusMedDbContext db)
    {
        _db = db;
    }

    public async Task<Study?> GetByUidAsync(string studyInstanceUid)
    {
        return await _db.Studies
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Patient)
            .Include(s => s.SeriesList)
            .FirstOrDefaultAsync(s => s.StudyInstanceUid == studyInstanceUid);
    }

    public async Task<IReadOnlyList<Study>> GetRecentAsync(int count = 50)
    {
        return await _db.Studies
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .Include(s => s.Patient)
            .ToListAsync();
    }

    public async Task<Study> AddAsync(Study study)
    {
        _db.Studies.Add(study);
        await _db.SaveChangesAsync();
        return study;
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetCountAsync()
    {
        return await _db.Studies.AsNoTracking().CountAsync();
    }

    public async Task<IReadOnlyList<ModalityCount>> GetModalityBreakdownAsync()
    {
        return await _db.Studies
            .AsNoTracking()
            .GroupBy(s => s.Modality)
            .Select(g => new ModalityCount { Modality = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Study>> GetFilteredAsync(string? search, string? modality, StudyStatus? status, DateTime? date, int maxResults = 500)
    {
        IQueryable<Study> query = _db.Studies
            .AsNoTracking()
            .Include(s => s.Patient);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim();
            query = query.Where(s =>
                EF.Functions.Like(s.Patient.Name, $"%{cleanSearch}%") ||
                EF.Functions.Like(s.Patient.PatientId, $"%{cleanSearch}%") ||
                EF.Functions.Like(s.AccessionNumber, $"%{cleanSearch}%") ||
                EF.Functions.Like(s.StudyDescription, $"%{cleanSearch}%"));
        }

        if (!string.IsNullOrWhiteSpace(modality) && !modality.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Modality == modality);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        if (date.HasValue)
        {
            var startDate = date.Value.Date;
            var endDate = startDate.AddDays(1);
            query = query.Where(s => s.StudyDate >= startDate && s.StudyDate < endDate);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetDistinctModalitiesAsync()
    {
        return await _db.Studies
            .AsNoTracking()
            .Where(s => !string.IsNullOrEmpty(s.Modality))
            .Select(s => s.Modality)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();
    }
}
