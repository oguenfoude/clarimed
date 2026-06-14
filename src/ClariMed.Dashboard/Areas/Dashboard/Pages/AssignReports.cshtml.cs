using ClariMed.Data;
using ClariMed.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Dashboard.Areas.Dashboard.Pages;

public class AssignReportsModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AssignReportsModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public List<Document> UnassignedReports { get; set; } = new();
    public List<Study> RecentStudies { get; set; } = new();

    public async Task OnGetAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        UnassignedReports = await db.Documents
            .Where(d => d.StudyId == null)
            .OrderByDescending(d => d.ReceivedAt)
            .ToListAsync();

        RecentStudies = await db.Studies
            .Include(s => s.Patient)
            .OrderByDescending(s => s.CreatedAt)
            .Take(100)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAssignAsync(int documentId, int studyId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        var doc = await db.Documents.FindAsync(documentId);
        var study = await db.Studies.FindAsync(studyId);

        if (doc != null && study != null)
        {
            doc.StudyId = studyId;

            // Copy the PDF into the study folder
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var targetPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "data", "studies", study.StudyInstanceUid));
            
            // If running in development (has bin/Debug), adjust path to project root
            if (baseDir.Contains(Path.Combine("bin", "Debug")))
            {
                var projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
                if (projectRoot != null)
                {
                    targetPath = Path.Combine(projectRoot, "data", "studies", study.StudyInstanceUid);
                }
            }

            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

            var newFile = Path.Combine(targetPath, "report_" + documentId + ".pdf");
            if (System.IO.File.Exists(doc.OriginalFilePath))
            {
                try
                {
                    System.IO.File.Copy(doc.OriginalFilePath, newFile, true);
                    doc.OriginalFilePath = newFile;
                    doc.PdfFilePath = newFile;
                }
                catch { }
            }



            await db.SaveChangesAsync();
        }

        return RedirectToPage("/Preview", new { area = "Dashboard", id = studyId });
    }

    public async Task<IActionResult> OnGetSearchAsync(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Partial("Shared/_InboxSearchResults", new List<Study>());

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        q = q.ToLower();
        var studies = await db.Studies
            .Include(s => s.Patient)
            .Where(s => s.Patient.Name.ToLower().Contains(q) || s.Patient.PatientId.ToLower().Contains(q))
            .OrderByDescending(s => s.StudyDate)
            .Take(10)
            .ToListAsync();

        return Partial("Shared/_InboxSearchResults", studies);
    }
}