using ClariMed.Data.Models;

namespace ClariMed.Data.Services;

public interface IClinicSettingsRepository
{
    Task<ClinicSettings> GetAsync();
    Task UpdateAsync(ClinicSettings settings);
}
