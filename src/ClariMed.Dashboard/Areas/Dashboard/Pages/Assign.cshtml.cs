using ClariMed.Data;
using ClariMed.Data.Models;
using ClariMed.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Dashboard.Areas.Dashboard.Pages;

public class AssignModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public InboxDocument? InboxDocument { get; set; }
    public List<AssignStudyRow> Studies { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int InboxDocId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string AssignmentFilter { get; set; } = "unassigned";

    public AssignModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<IActionResult> OnGetAsync(int inboxDocId, string? search)
    {
        InboxDocId = inboxDocId;
        Search = search;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

        InboxDocument = await db.InboxDocuments.FindAsync(inboxDocId);
        if (InboxDocument == null)
        {
            TempData["ErrorMessage"] = "Document not found.";
            return Page();
        }

        var query = db.Studies.Include(s => s.Patient).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(s => s.Patient.Name.ToLower().Contains(q) || s.Patient.PatientId.ToLower().Contains(q));
        }

        if (AssignmentFilter == "unassigned")
        {
            query = query.Where(s => !db.Documents.Any(d => d.StudyId == s.Id));
        }
        else if (AssignmentFilter == "assigned")
        {
            query = query.Where(s => db.Documents.Any(d => d.StudyId == s.Id));
        }

        var rawStudies = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .Select(s => new { Study = s, IsAssigned = db.Documents.Any(d => d.StudyId == s.Id) })
            .ToListAsync();

        Studies = rawStudies.Select(x => new AssignStudyRow
        {
            Id = x.Study.Id,
            PatientName = x.Study.Patient?.Name ?? "Unknown",
            PatientId = x.Study.Patient?.PatientId ?? "Unknown",
            Modality = x.Study.Modality,
            AccessionNumber = x.Study.AccessionNumber,
            ImageCount = x.Study.ImageCount,
            StudyDate = x.Study.StudyDate,
            Status = x.Study.Status,
            CreatedAt = x.Study.CreatedAt,
            IsAssigned = x.IsAssigned
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnGetTableAsync(int inboxDocId, string? search)
    {
        InboxDocId = inboxDocId;
        Search = search;
        await OnGetAsync(inboxDocId, search);
        return Partial("Shared/_AssignPatientsTable", this);
    }

    public async Task<IActionResult> OnPostAssignAsync(int inboxDocId, int studyId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
        var inboxRepo = scope.ServiceProvider.GetRequiredService<IInboxDocumentRepository>();

        var inboxDoc = await db.InboxDocuments.FindAsync(inboxDocId);
        if (inboxDoc == null || inboxDoc.Status != InboxDocumentStatus.Unassigned)
        {
            TempData["ErrorMessage"] = "The document is no longer available or was already assigned.";
            return RedirectToPage(new { inboxDocId });
        }

        var study = await db.Studies.FindAsync(studyId);
        if (study == null)
        {
            TempData["ErrorMessage"] = "Selected study was not found.";
            return RedirectToPage(new { inboxDocId });
        }

        // Assign the inbox document to the study in the inbox table
        await inboxRepo.AssignToStudyAsync(inboxDocId, studyId);

        // Create a Document record in the database representing this printed PDF
        var doc = new Document
        {
            OriginalFileName = inboxDoc.FileName,
            OriginalFilePath = inboxDoc.PdfPath,
            PdfFilePath = inboxDoc.PdfPath,
            Status = DocumentStatus.Converted,
            ReceivedAt = inboxDoc.ReceivedAt,
            ConvertedAt = DateTime.UtcNow,
            StudyId = studyId
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync(); // save to generate doc.Id



        TempData["InfoMessage"] = "Document assigned successfully.";
        return RedirectToPage("/Preview", new { area = "Dashboard", id = studyId });
    }
}

public class AssignStudyRow : StudyRow
{
    public bool IsAssigned { get; set; }
}