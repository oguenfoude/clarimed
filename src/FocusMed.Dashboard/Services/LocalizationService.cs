using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using FocusMed.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;

namespace FocusMed.Dashboard.Services;

public interface ILocalizationService
{
    string this[string key] { get; }
    string GetString(string key);
}

public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, string> _en = new();
    private readonly Dictionary<string, string> _fr = new();
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalizationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        LoadLanguage("en", _en);
        LoadLanguage("fr", _fr);
    }

    private void LoadLanguage(string lang, Dictionary<string, string> target)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", $"{lang}.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    target[kvp.Key] = kvp.Value;
                }
            }
        }
    }

    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        // Default to English, but we should read from ClinicSettings if possible
        // To keep it simple and thread-safe inside Razor Pages, we grab the scoped DB context
        // and fetch the language. We cache it per request if possible, but reading DB is fast enough for now
        // Actually, let's grab it from HttpContext Items to avoid multiple DB calls per page load
        string lang = "en";
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            if (context.Items.TryGetValue("CurrentLanguage", out var langObj) && langObj is string l)
            {
                lang = l;
            }
            else
            {
                // Fallback to fetch from DB
                var scopeFactory = context.RequestServices.GetRequiredService<IServiceScopeFactory>();
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
                var settings = repo.GetAsync().GetAwaiter().GetResult();
                lang = settings.Language ?? "en";
                context.Items["CurrentLanguage"] = lang;
            }
        }

        var targetDict = lang.StartsWith("fr") ? _fr : _en;
        return targetDict.TryGetValue(key, out var value) ? value : key;
    }
}
