using ClariMed.Data;
using ClariMed.Data.Models;
using ClariMed.Data.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Dashboard.Areas.Dashboard.Pages;

[IgnoreAntiforgeryToken]
public class SettingsModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ClinicSettings? Settings { get; set; }
    public string Lang { get; set; } = "en";

    public SettingsModel(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task OnGetAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
        Settings = await repo.GetAsync();
        Lang = Settings?.Language ?? "en";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var lang = Request.Form["Language"].FirstOrDefault() ?? "en";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
            var settings = await repo.GetAsync();
            settings.Language = lang;
            await repo.UpdateAsync(settings);
            
            // Note: Since we are using an HTMX form submission, returning localized strings here 
            // is tricky if the language changes in the same request. We can just return a generic success 
            // or trigger a page reload via HX-Refresh.
            Response.Headers["HX-Refresh"] = "true";
            return Content("");
        }
        catch (Exception ex)
        {
            return Content("<span class='text-red-600 text-sm font-medium'>Error: " + ex.Message + "</span>");
        }
    }
}