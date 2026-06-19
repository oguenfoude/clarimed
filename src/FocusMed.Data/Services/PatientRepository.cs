using FocusMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Data.Services;

public class PatientRepository : IPatientRepository
{
    private readonly FocusMedDbContext _db;

    public PatientRepository(FocusMedDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetCountAsync()
    {
        return await _db.Patients.CountAsync();
    }
}
