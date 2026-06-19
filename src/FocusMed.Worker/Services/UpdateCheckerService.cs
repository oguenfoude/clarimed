using System.Text.Json;
using FocusMed.Data;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Worker.Services;

public class UpdateCheckerService : BackgroundService
{
    private readonly ILogger<UpdateCheckerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _pollInterval = TimeSpan.FromHours(1);
    private const string CurrentVersion = "v1.0.0";
    private const string RepoUrl = "https://api.github.com/repos/microsoft/PowerToys/releases/latest"; // Dummy repo for testing

    public UpdateCheckerService(
        ILogger<UpdateCheckerService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "FocusMed-UpdateChecker");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UpdateCheckerService started — checking for updates every {Interval} hours.", _pollInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdatesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking for updates.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("UpdateCheckerService stopped.");
    }

    private async Task CheckForUpdatesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();

        var settings = await db.ClinicSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (settings == null) return;

        var response = await _httpClient.GetAsync(RepoUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to check for updates. GitHub API returned {StatusCode}", response.StatusCode);
            return;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.TryGetProperty("tag_name", out var tagElement))
        {
            var latestVersion = tagElement.GetString();
            if (!string.IsNullOrEmpty(latestVersion) && latestVersion != CurrentVersion)
            {
                if (!settings.UpdateAvailable || settings.LatestVersion != latestVersion)
                {
                    _logger.LogInformation("New update available: {LatestVersion} (Current: {CurrentVersion})", latestVersion, CurrentVersion);
                    settings.UpdateAvailable = true;
                    settings.LatestVersion = latestVersion;
                    await db.SaveChangesAsync(ct);
                }
            }
            else
            {
                if (settings.UpdateAvailable)
                {
                    settings.UpdateAvailable = false;
                    settings.LatestVersion = string.Empty;
                    await db.SaveChangesAsync(ct);
                }
            }
        }
    }
}
