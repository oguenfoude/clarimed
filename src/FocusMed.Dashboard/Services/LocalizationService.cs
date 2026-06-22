using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
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
        string lang = "en";
        var context = _httpContextAccessor.HttpContext;
        if (context != null && context.Items.TryGetValue("CurrentLanguage", out var langObj) && langObj is string l)
        {
            lang = l;
        }

        var targetDict = lang.StartsWith("fr") ? _fr : _en;
        return targetDict.TryGetValue(key, out var value) ? value : key;
    }
}
