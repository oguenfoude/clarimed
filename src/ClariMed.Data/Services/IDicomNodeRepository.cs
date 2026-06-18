using ClariMed.Data.Models;

namespace ClariMed.Data.Services;

public interface IDicomNodeRepository
{
    Task<IReadOnlyList<DicomNode>> GetAllAsync();
    Task<DicomNode?> GetByIdAsync(int id);
    Task<DicomNode> AddAsync(DicomNode node);
    Task UpdateAsync(DicomNode node);
    Task DeleteAsync(int id);
}
