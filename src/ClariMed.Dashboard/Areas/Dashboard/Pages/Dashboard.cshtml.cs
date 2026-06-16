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

namespace ClariMed.Dashboard.Areas.Dashboard.Pages;

[IgnoreAntiforgeryToken]
public class DashboardModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public List<StudyRow> Studies { get; set; } = new();
    public int TotalPatients { get; set; }
    public int TotalStudies { get; set; }
    public int TotalImages { get; set; }
    public int TotalPending { get; set; }
    public List<ModalityCount> ModalityBreakdown { get; set; } = new();

    public DashboardModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task OnGetAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
        await LoadDataAsync(db);
    }

    public async Task<IActionResult> OnGetTableAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
        await LoadDataAsync(db);
        return Partial("Shared/_StudiesTable", this);
    }



    public async Task<IActionResult> OnGetCheckInboxAsync(string? dismissed)
    {
        // Prevent infinite popups if the user is already on the assignment page
        if (Request.Headers.TryGetValue("HX-Current-URL", out var currentUrl))
        {
            if (currentUrl.ToString().Contains("/dashboard/assign") || currentUrl.ToString().Contains("/inbox"))
                return new EmptyResult();
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
        
        var dismissedIds = new List<int>();

        var unassigned = await db.InboxDocuments
            .Where(d => d.Status == InboxDocumentStatus.Unassigned && !dismissedIds.Contains(d.Id))
            .OrderByDescending(d => d.ReceivedAt)
            .FirstOrDefaultAsync();

        if (unassigned != null)
        {
            return Content($@"
                <div id='inbox-modal-overlay' class='fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm transition-opacity'>
                    <div class='bg-white rounded-2xl shadow-2xl w-full max-w-md p-6 border border-slate-200 transform transition-all scale-100 opacity-100'>
                        <div class='flex items-center gap-4 mb-4'>
                            <div class='w-12 h-12 rounded-full bg-blue-100 text-blue-600 flex items-center justify-center shrink-0'>
                                <svg class='w-6 h-6' fill='none' stroke='currentColor' viewBox='0 0 24 24'>
                                    <path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z'/>
                                </svg>
                            </div>
                            <div>
                                <h3 class='text-lg font-bold text-slate-900'>New Incoming Document</h3>
                                <p class='text-sm text-slate-500'>Received and waiting for assignment</p>
                            </div>
                        </div>
                        <p class='text-sm text-slate-600 mb-6'>
                            <strong class='text-slate-800'>{unassigned.FileName}</strong> is waiting to be assigned to a patient study.
                        </p>
                        <div class='flex gap-3 justify-end'>
                            <button hx-post='/dashboard?handler=DismissInbox&id={unassigned.Id}' hx-target='#inbox-modal-overlay' hx-swap='delete' class='px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-100 rounded-lg transition-colors'>Dismiss</button>
                            <a href='/dashboard/assign?inboxDocId={unassigned.Id}' class='px-4 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-sm transition-colors'>Assign Now</a>
                        </div>
                    </div>
                </div>
            ", "text/html");
        }

        return new EmptyResult();
    }

    public async Task<IActionResult> OnPostDismissInboxAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
        var doc = await db.InboxDocuments.FindAsync(id);
        if (doc != null)
        {
            try { if (System.IO.File.Exists(doc.PdfPath)) System.IO.File.Delete(doc.PdfPath); } catch { }
            db.InboxDocuments.Remove(doc);
            await db.SaveChangesAsync();
        }
        return new EmptyResult();
    }

    private async Task LoadDataAsync(ClariMedDbContext db)
    {
        TotalPatients = await db.Patients.CountAsync();
        TotalStudies = await db.Studies.CountAsync();
        TotalImages = await db.Studies.SumAsync(s => s.ImageCount);
        TotalPending = 0;

        var startOfToday = DateTime.Today;

        // Modality Distribution (Today only)
        ModalityBreakdown = await db.Studies
            .Where(s => s.CreatedAt >= startOfToday)
            .GroupBy(s => s.Modality)
            .Select(g => new ModalityCount
            {
                Modality = string.IsNullOrEmpty(g.Key) ? "Unknown" : g.Key,
                Count = g.Count()
            })
            .OrderByDescending(m => m.Count)
            .ToListAsync();

        // Recent studies (Today only, newest to oldest)
        var rawStudies = await db.Studies
            .Include(s => s.Patient)
            .Where(s => s.CreatedAt >= startOfToday)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync();

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
