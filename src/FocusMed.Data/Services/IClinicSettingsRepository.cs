using FocusMed.Data.Models;

namespace FocusMed.Data.Services;

public interface IClinicSettingsRepository
{
    Task<ClinicSettings> GetAsync();
    Task UpdateAsync(ClinicSettings settings);
}
