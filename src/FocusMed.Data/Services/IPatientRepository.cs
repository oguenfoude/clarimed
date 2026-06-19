using FocusMed.Data.Models;

namespace FocusMed.Data.Services;

public interface IPatientRepository
{
    Task<int> GetCountAsync();
}
