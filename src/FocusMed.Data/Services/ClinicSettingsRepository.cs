using FocusMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Data.Services;

public class ClinicSettingsRepository : IClinicSettingsRepository
{
    private readonly FocusMedDbContext _db;

    public ClinicSettingsRepository(FocusMedDbContext db)
    {
        _db = db;
    }

    public async Task<ClinicSettings> GetAsync()
    {
        var settings = await _db.ClinicSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new ClinicSettings();
            _db.ClinicSettings.Add(settings);
            await _db.SaveChangesAsync();
        }
        return settings;
    }

    public async Task UpdateAsync(ClinicSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _db.ClinicSettings.Update(settings);
        await _db.SaveChangesAsync();
    }
}
