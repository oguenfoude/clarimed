using ClariMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ClariMed.Data.Services;

public class PatientRepository : IPatientRepository
{
    private readonly ClariMedDbContext _db;

    public PatientRepository(ClariMedDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetCountAsync()
    {
        return await _db.Patients.CountAsync();
    }
}
