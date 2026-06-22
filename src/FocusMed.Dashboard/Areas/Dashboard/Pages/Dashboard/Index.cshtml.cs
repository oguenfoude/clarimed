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

[IgnoreAntiforgeryToken]
public class DashboardModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public int TotalPatients { get; set; }
    public List<ModalityCount> ModalityBreakdown { get; set; } = new();

    public DashboardModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task OnGetAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();
        await LoadDataAsync(db);
    }

    private async Task LoadDataAsync(FocusMedDbContext db)
    {
        TotalPatients = await db.Patients.CountAsync();

        var startOfToday = DateTime.Today;

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
