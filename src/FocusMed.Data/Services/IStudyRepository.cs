using FocusMed.Data.Models;

namespace FocusMed.Data.Services;

public interface IStudyRepository
{
    Task<Study?> GetByUidAsync(string studyInstanceUid);
    Task<IReadOnlyList<Study>> GetRecentAsync(int count = 50);
    Task<Study> AddAsync(Study study);
    Task SaveChangesAsync();
    Task<int> GetCountAsync();
    Task<IReadOnlyList<ModalityCount>> GetModalityBreakdownAsync();
    Task<IReadOnlyList<Study>> GetFilteredAsync(string? search, string? modality, StudyStatus? status, DateTime? date, int maxResults = 500);
    Task<IReadOnlyList<string>> GetDistinctModalitiesAsync();
}
