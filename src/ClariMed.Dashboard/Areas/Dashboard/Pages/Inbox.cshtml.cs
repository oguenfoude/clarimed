using ClariMed.Data;
using ClariMed.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Dashboard.Areas.Dashboard.Pages;

public class InboxModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public List<Document> UnassignedDocuments { get; set; } = new();

    public InboxModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task OnGetAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        UnassignedDocuments = await db.Documents
            .Where(d => d.StudyId == null && d.Status == DocumentStatus.Converted)
            .OrderByDescending(d => d.ReceivedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnGetSearchPatientsAsync(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Partial("Shared/_InboxSearchResults", new List<Study>());

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        q = q.ToLower();
        var studies = await db.Studies
            .Include(s => s.Patient)
            .Where(s => !db.Documents.Any(d => d.StudyId == s.Id))
            .Where(s => s.Patient.Name.ToLower().Contains(q) || s.Patient.PatientId.ToLower().Contains(q))
            .OrderByDescending(s => s.StudyDate)
            .Take(10)
            .ToListAsync();

        return Partial("Shared/_InboxSearchResults", studies);
    }

    public async Task<IActionResult> OnPostAttachAsync(int documentId, int studyId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        var doc = await db.Documents.FindAsync(documentId);
        var study = await db.Studies.FindAsync(studyId);

        if (doc != null && study != null)
        {
            doc.StudyId = study.Id;

            await db.SaveChangesAsync();
        }

        return RedirectToPage("/Preview", new { area = "Dashboard", id = studyId });
    }
}