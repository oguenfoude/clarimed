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
        var resume = Request.Form["ResumeText"].FirstOrDefault() ?? "";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
            var settings = await repo.GetAsync();
            settings.Language = lang;
            settings.ResumeText = resume;
            await repo.UpdateAsync(settings);
            return Content("<span class='text-green-600 text-sm font-medium'>" + T("SavedOk") + "</span>");
        }
        catch (Exception ex)
        {
            return Content("<span class='text-red-600 text-sm font-medium'>Error: " + ex.Message + "</span>");
        }
    }

    public string T(string key)
    {
        if (Lang == "fr")
        {
            return key switch
            {
                "Settings" => "Paramètres",
                "SettingsDesc" => "Configurez votre installation",
                "LanguageLabel" => "Langue",
                "ResumeLabel" => "Rapport médical",
                "ResumeDesc" => "Texte qui apparaîtra entre la page de garde et les images dans le PDF",
                "Save" => "Enregistrer",
                "SavedOk" => "✓ Enregistré",
                _ => key
            };
        }
        return key switch
        {
            "Settings" => "Settings",
            "SettingsDesc" => "Configure your installation",
            "LanguageLabel" => "Language",
            "ResumeLabel" => "Medical Report",
            "ResumeDesc" => "Text that appears between the cover page and images in the PDF",
            "Save" => "Save",
            "SavedOk" => "✓ Saved",
            _ => key
        };
    }
}