using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FocusMed.Data;
using FocusMed.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Dashboard.Areas.Dashboard.Pages;

public class PatientsModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public List<StudyRow> Studies { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EndDate { get; set; }

    public PatientsModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task OnGetAsync(string? search, string? modality, string? status, string? startDate, string? endDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();
        await LoadAsync(db, search, modality, status, startDate, endDate, isInitialLoad: Request.Query.Count == 0);
    }

    public async Task<IActionResult> OnGetTableAsync(string? search, string? modality, string? status, string? startDate, string? endDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();
        await LoadAsync(db, search, modality, status, startDate, endDate, isInitialLoad: false);
        return Partial("Shared/_PatientsTable", this);
    }

    private async Task LoadAsync(FocusMedDbContext db, string? search, string? modality, string? status, string? startDate, string? endDate, bool isInitialLoad)
    {
        StartDate = startDate;
        EndDate = endDate;

        var query = db.Studies.Include(s => s.Patient).AsQueryable();

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

        query = query.OrderByDescending(s => s.StudyDate ?? s.CreatedAt);

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
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

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

public class StudyRow
{
    public int Id { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public int ImageCount { get; set; }
    public DateTime? StudyDate { get; set; }
    public StudyStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
