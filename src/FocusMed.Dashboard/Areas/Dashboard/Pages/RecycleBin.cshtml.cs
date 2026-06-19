using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FocusMed.Data;
using FocusMed.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Dashboard.Areas.Dashboard.Pages;

public class RecycleBinModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public List<StudyRow> DeletedStudies { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    public RecycleBinModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task OnGetAsync(string? search, DateTime? startDate, DateTime? endDate)
    {
        Search = search;
        StartDate = startDate;
        EndDate = endDate;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

        var query = db.Studies
            .IgnoreQueryFilters()
            .Include(s => s.Patient)
            .Where(s => s.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(s => s.Patient.Name.ToLower().Contains(q) || s.Patient.PatientId.ToLower().Contains(q));
        }

        if (startDate.HasValue)
        {
            query = query.Where(s => s.StudyDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.StudyDate <= endDate.Value);
        }

        query = query.OrderByDescending(s => s.DeletedAt);

        var rawStudies = await query.ToListAsync();

        DeletedStudies = rawStudies.Select(s => new StudyRow
        {
            Id = s.Id,
            PatientName = s.Patient?.Name ?? "Unknown",
            PatientId = s.Patient?.PatientId ?? "Unknown",
            Modality = s.Modality,
            AccessionNumber = s.AccessionNumber,
            ImageCount = s.ImageCount,
            StudyDate = s.StudyDate,
            Status = s.Status,
            CreatedAt = s.DeletedAt ?? s.CreatedAt // Using CreatedAt to display DeletedAt time
        }).ToList();
    }

    public async Task<IActionResult> OnGetTableAsync(string? search, DateTime? startDate, DateTime? endDate)
    {
        await OnGetAsync(search, startDate, endDate);
        return Partial("Shared/_RecycleBinTable", this);
    }

    public async Task<IActionResult> OnPostRestoreAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

        var study = await db.Studies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (study != null)
        {
            study.IsDeleted = false;
            study.DeletedAt = null;
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostForceDeleteAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

        var study = await db.Studies
            .IgnoreQueryFilters()
            .AsSplitQuery()
            .Include(s => s.Patient)
            .Include(s => s.SeriesList)
                .ThenInclude(series => series.Images)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (study == null) return RedirectToPage();

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var archivePath = Path.GetFullPath(config["FocusMed:ArchivePath"] ?? "data/archive");
        var imagesPath = Path.Combine(Path.GetDirectoryName(archivePath) ?? "data", "images");

        // 1. Delete physical DICOM and PNG files
        foreach (var series in study.SeriesList)
        {
            foreach (var img in series.Images)
            {
                if (!string.IsNullOrEmpty(img.FilePath) && System.IO.File.Exists(img.FilePath))
                {
                    try { System.IO.File.Delete(img.FilePath); } catch { /* ignore */ }
                    
                    var pngPath = img.FilePath.Replace(".dcm", ".png").Replace("archive", "images");
                    if (System.IO.File.Exists(pngPath))
                    {
                        try { System.IO.File.Delete(pngPath); } catch { /* ignore */ }
                    }
                }
            }
        }

        // 2. Delete linked Document PDFs (if any)
        var docs = await db.Documents.Where(d => d.StudyId == id).ToListAsync();
        foreach (var doc in docs)
        {
            if (!string.IsNullOrEmpty(doc.PdfFilePath) && System.IO.File.Exists(doc.PdfFilePath))
            {
                try { System.IO.File.Delete(doc.PdfFilePath); } catch { /* ignore */ }
            }
        }

        // Let EF cascade delete the DB rows (Images, Series, Documents, etc.)
        db.Studies.Remove(study);
        await db.SaveChangesAsync();

        return RedirectToPage();
    }
}
