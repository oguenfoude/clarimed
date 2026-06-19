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

    public async Task<IReadOnlyList<Study>> GetByPatientAsync(int patientId)
    {
        return await _db.Studies
            .AsNoTracking()
            .Where(s => s.PatientId == patientId)
            .OrderByDescending(s => s.StudyDate)
            .Include(s => s.Patient)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Study>> GetReceivingStudiesOlderThanAsync(TimeSpan stabilizationWindow)
    {
        var cutoff = DateTime.UtcNow - stabilizationWindow;
        return await _db.Studies
            .Where(s => s.Status == StudyStatus.Receiving && s.LastImageReceivedAt < cutoff)
            .Include(s => s.Patient)
            .OrderBy(s => s.LastImageReceivedAt)
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
        return await _db.Studies.CountAsync();
    }

    public async Task<int> GetTotalImageCountAsync()
    {
        return await _db.Images.CountAsync();
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
            var cleanSearch = search.Trim().ToLower();
            query = query.Where(s => 
                s.Patient.Name.ToLower().Contains(cleanSearch) ||
                s.Patient.PatientId.ToLower().Contains(cleanSearch) ||
                s.AccessionNumber.ToLower().Contains(cleanSearch) ||
                s.StudyDescription.ToLower().Contains(cleanSearch));
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
