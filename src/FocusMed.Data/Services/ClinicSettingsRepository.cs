using FocusMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Data.Services;

public class ClinicSettingsRepository : IClinicSettingsRepository
{
    private readonly FocusMedDbContext _db;
    private static ClinicSettings? _cached;
    private static readonly object _lock = new();
    private static DateTime _lastFetch = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public ClinicSettingsRepository(FocusMedDbContext db)
    {
        _db = db;
    }

    public async Task<ClinicSettings> GetAsync()
    {
        lock (_lock)
        {
            if (_cached != null && DateTime.UtcNow - _lastFetch < CacheDuration)
                return _cached;
        }

        var settings = await _db.ClinicSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new ClinicSettings();
            _db.ClinicSettings.Add(settings);
            await _db.SaveChangesAsync();
        }

        lock (_lock)
        {
            _cached = settings;
            _lastFetch = DateTime.UtcNow;
        }

        return settings;
    }

    public void InvalidateCache()
    {
        lock (_lock)
        {
            _cached = null;
            _lastFetch = DateTime.MinValue;
        }
    }

    public async Task UpdateAsync(ClinicSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _db.ClinicSettings.Update(settings);
        await _db.SaveChangesAsync();
        InvalidateCache();
    }
}
