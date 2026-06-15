using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClariMed.Data;
using ClariMed.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace ClariMed.Dashboard.Areas.Dashboard.Pages;

public class PatientsModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public List<StudyRow> Studies { get; set; } = new();
    public List<string> Modalities { get; set; } = new();
    public int TotalStudies { get; set; }
    public int TotalImages { get; set; }
    public int Receiving { get; set; }
    public int Complete { get; set; }

    public PatientsModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task OnGetAsync(string? search, string? modality, string? status, string? startDate, string? endDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
        await LoadAsync(db, search, modality, status, startDate, endDate);
    }

    public async Task<IActionResult> OnGetTableAsync(string? search, string? modality, string? status, string? startDate, string? endDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
        await LoadAsync(db, search, modality, status, startDate, endDate);
        return Partial("Shared/_PatientsTable", this);
    }

    private async Task LoadAsync(ClariMedDbContext db, string? search, string? modality, string? status, string? startDate, string? endDate)
    {
        // Global stats
        TotalStudies = await db.Studies.CountAsync();
        TotalImages = await db.Studies.SumAsync(s => s.ImageCount);
        Receiving = await db.Studies.CountAsync(s => s.Status == StudyStatus.Receiving);
        Complete = await db.Studies.CountAsync(s => s.Status == StudyStatus.Complete);

        Modalities = await db.Studies
            .Select(s => s.Modality)
            .Distinct()
            .Where(m => !string.IsNullOrEmpty(m))
            .ToListAsync();

        var query = db.Studies.Include(s => s.Patient).AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchClean = search.Trim().ToLower();
            query = query.Where(s => s.Patient.Name.ToLower().Contains(searchClean) || 
                                     s.Patient.PatientId.ToLower().Contains(searchClean) || 
                                     s.AccessionNumber.ToLower().Contains(searchClean));
        }

        if (!string.IsNullOrWhiteSpace(modality))
        {
            query = query.Where(s => s.Modality == modality);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<StudyStatus>(status, out var statusEnum))
            {
                query = query.Where(s => s.Status == statusEnum);
            }
        }

        if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var parsedStart))
        {
            var localStart = parsedStart.Date;
            query = query.Where(s => s.StudyDate >= localStart);
        }

        if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var parsedEnd))
        {
            var localEnd = parsedEnd.Date.AddDays(1);
            query = query.Where(s => s.StudyDate < localEnd);
        }

        // Indexing from old to new (ascending order)
        query = query.OrderBy(s => s.StudyDate ?? s.CreatedAt);

        var rawStudies = await query.Take(500).ToListAsync();

        Studies = rawStudies.Select(s => new StudyRow
        {
            Id = s.Id,
            PatientName = s.Patient?.Name ?? "Unknown",
            PatientId = s.Patient?.PatientId ?? "Unknown",
            Modality = s.Modality,
            AccessionNumber = s.AccessionNumber,
            ImageCount = s.ImageCount,
            StudyDate = s.StudyDate,
            Status = s.Status,
            CreatedAt = s.CreatedAt
        }).ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        var study = await db.Studies
            .FirstOrDefaultAsync(s => s.Id == id);

        if (study != null)
        {
            study.IsDeleted = true;
            study.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}