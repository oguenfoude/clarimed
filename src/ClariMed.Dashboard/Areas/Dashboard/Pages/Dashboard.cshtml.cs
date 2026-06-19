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

    }
}
