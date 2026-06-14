using ClariMed.Data.Models;

namespace ClariMed.Data.Services;

public interface IPatientRepository
{
    Task<int> GetCountAsync();
}
