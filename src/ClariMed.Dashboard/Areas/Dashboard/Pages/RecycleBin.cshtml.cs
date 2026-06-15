using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClariMed.Data;
using ClariMed.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Dashboard.Areas.Dashboard.Pages;

public class RecycleBinModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public List<StudyRow> DeletedStudies { get; set; } = new();

    public RecycleBinModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task OnGetAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        var query = db.Studies
            .IgnoreQueryFilters()
            .Include(s => s.Patient)
            .Where(s => s.IsDeleted)
            .OrderByDescending(s => s.DeletedAt);

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

    public async Task<IActionResult> OnPostRestoreAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

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
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        var study = await db.Studies
            .IgnoreQueryFilters()
            .Include(s => s.Patient)
            .Include(s => s.SeriesList)
                .ThenInclude(series => series.Images)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (study == null) return RedirectToPage();

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var archivePath = Path.GetFullPath(config["ClariMed:ArchivePath"] ?? "data/archive");
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

        // 3. Delete linked InboxDocument PDFs (if any)
        var inboxDocs = await db.InboxDocuments.Where(d => d.AssignedToStudyId == id).ToListAsync();
        foreach (var doc in inboxDocs)
        {
            if (!string.IsNullOrEmpty(doc.PdfPath) && System.IO.File.Exists(doc.PdfPath))
            {
                try { System.IO.File.Delete(doc.PdfPath); } catch { /* ignore */ }
            }
        }

        // Let EF cascade delete the DB rows (Images, Series, Documents, etc.)
        db.Studies.Remove(study);
        await db.SaveChangesAsync();

        return RedirectToPage();
    }
}
