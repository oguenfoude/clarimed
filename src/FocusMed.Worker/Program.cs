using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using FocusMed.Data;
using FocusMed.Data.Models;
using FocusMed.Data.Services;
using FocusMed.Dicom;
using FocusMed.Documents;
using FocusMed.Printing;
using FocusMed.Worker.Services;
using FocusMed.Dashboard.Api;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;


var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
if (!string.IsNullOrEmpty(exePath))
{
    var exeDir = Path.GetDirectoryName(exePath);
    if (!string.IsNullOrEmpty(exeDir))
    {
        Directory.SetCurrentDirectory(exeDir);
    }
}

var builder = WebApplication.CreateBuilder(args);

// ── Global Exception Handlers (Crash Safety) ──
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    var exception = e.ExceptionObject as Exception;
    Console.WriteLine($"[CRITICAL] Unhandled Exception: {exception?.Message}");
    // Do not auto-restart, let Windows Service Manager or Docker handle it if configured,
    // but the application itself gracefully stops.
    Environment.Exit(1);
};

TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    Console.WriteLine($"[CRITICAL] Unobserved Task Exception: {e.Exception?.Message}");
    e.SetObserved();
    Environment.Exit(1);
};

// ── Serve on port 5000 ──
builder.WebHost.UseUrls("http://*:5000");

// ── Configure Terminal Logging ──
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "[HH:mm:ss] ";
    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
});

// ── Windows Service support ──
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "FocusMed";
});

// ── Safe Data Path Resolver ──
string GetSafeDataPath(string? configuredPath, string defaultRelative)
{
    var path = configuredPath ?? defaultRelative;
    if (!Path.IsPathRooted(path))
    {
        var commonAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FocusMed");
        path = Path.Combine(commonAppData, path);
    }
    return Path.GetFullPath(path);
}

// ── Read configuration ──
var config = builder.Configuration.GetSection("FocusMed");
var databasePath = GetSafeDataPath(config.GetValue<string>("DatabasePath"), "data/db/focusmed.db");
var watchFolderPath = GetSafeDataPath(config.GetValue<string>("WatchFolderPath"), "data/WatchFolder");
var documentOutputPath = GetSafeDataPath(config.GetValue<string>("DocumentOutputPath"), "data/Documents");
var archivePath = GetSafeDataPath(config.GetValue<string>("ArchivePath"), "data/archive");
var imagesPath = GetSafeDataPath(config.GetValue<string>("ImagesPath"), "data/images");

// Write absolute paths back to config so DI services see them natively
config["DatabasePath"] = databasePath;
config["WatchFolderPath"] = watchFolderPath;
config["DocumentOutputPath"] = documentOutputPath;
config["ArchivePath"] = archivePath;
config["ImagesPath"] = imagesPath;

// ── Ensure essential directories exist ──
var dataDir = Path.GetDirectoryName(databasePath);
if (!string.IsNullOrEmpty(dataDir)) Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(watchFolderPath);
Directory.CreateDirectory(documentOutputPath);
Directory.CreateDirectory(archivePath);
Directory.CreateDirectory(imagesPath);

// ── Register Application Layers ──
builder.Services.AddFocusMedData(databasePath);
builder.Services.AddFocusMedDicom();
builder.Services.AddFocusMedDocuments();
builder.Services.AddFocusMedPrinting();

builder.Services.AddSingleton<FocusMed.Documents.Ingestion.IDocumentIngestionChannel>(sp => 
{
    var queue = sp.GetRequiredService<FocusMed.Documents.Ingestion.DocumentIngestionQueue>();
    var logger = sp.GetRequiredService<ILogger<FocusMed.Documents.Ingestion.WatchFolderIngestionChannel>>();
    return new FocusMed.Documents.Ingestion.WatchFolderIngestionChannel(queue, logger, watchFolderPath);
});

// ── Register Background Workers ──
builder.Services.AddHostedService<DicomListenerService>();
builder.Services.AddHostedService<DocumentProcessingService>();
builder.Services.AddHostedService<DocumentWatcherService>();
builder.Services.AddHostedService<StudyCompletionService>();
builder.Services.AddHostedService<RecycleBinCleanupService>();
builder.Services.AddHostedService<DicomRestartService>();
builder.Services.AddHostedService<UpdateCheckerService>();

// ── Authentication (Cookie-based) removed per user request ──

// ── Add Localization and Razor Pages support ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<FocusMed.Dashboard.Services.ILocalizationService, FocusMed.Dashboard.Services.LocalizationService>();
builder.Services.AddRazorPages();

var app = builder.Build();

// ── Initialize Database and License ──
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();
    db.Database.Migrate();
}

// ── Bridge DI for fo-dicom ──
FellowOakDicom.DicomSetupBuilder.UseServiceProvider(app.Services);

// ── Serve Static Assets ──
app.UseStaticFiles();

// Serve DICOM images
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesPath),
    RequestPath = "/dicom-images"
});

// Serve generated reports/documents
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(documentOutputPath)),
    RequestPath = "/documents"
});

app.Use(async (context, next) =>
{
    var scopeFactory = context.RequestServices.GetRequiredService<IServiceScopeFactory>();
    using var scope = scopeFactory.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<FocusMed.Data.Services.IClinicSettingsRepository>();
    var settings = await repo.GetAsync();
    var lang = settings.Language ?? "en";
    
    var culture = lang.StartsWith("fr", StringComparison.OrdinalIgnoreCase) 
        ? new System.Globalization.CultureInfo("fr-FR") 
        : new System.Globalization.CultureInfo("en-US");
        
    System.Globalization.CultureInfo.CurrentCulture = culture;
    System.Globalization.CultureInfo.CurrentUICulture = culture;
    context.Items["CurrentLanguage"] = lang;

    await next();
});

app.UseRouting();

// ── Authentication & Authorization middleware removed ──

// Redirect root to dashboard
app.MapGet("/", async context =>
{
    context.Response.Redirect("/dashboard");
    await Task.CompletedTask;
});

// System shutdown endpoint
app.MapGet("/system-stop", (IHostApplicationLifetime lifetime) =>
{
    // Run shutdown asynchronously so the HTTP response can be sent first
    Task.Run(async () => 
    {
        await Task.Delay(500);
        
        // Also kill the tray app UI so the ENTIRE suite completely stops
        foreach (var processName in new[] { "FocusMed", "FocusMed.Notifier" })
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (var p in processes)
            {
                try { p.Kill(); } catch { }
            }
        }

        lifetime.StopApplication();
    });
    return Results.Ok(new { status = "success", message = "Entire system (Backend & UI) is shutting down..." });
});

app.MapQuickAssignEndpoints();
app.MapPrintEndpoints();
app.MapDocumentEndpoints();
app.MapRazorPages();

// ── Auto-start Notifier tray app if not already running ──
try
{
    var existing = System.Diagnostics.Process.GetProcessesByName("FocusMed");
    if (existing.Length == 0)
    {
        // Check same directory first (production layout), then dev path (4 levels up from Worker output)
        var baseDir = AppContext.BaseDirectory;
        var sameDirPath = Path.Combine(baseDir, "FocusMed.exe");
        var devPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "FocusMed.Notifier", "bin", "Debug", "net10.0-windows", "FocusMed.exe"));

        var notifierPath = File.Exists(sameDirPath) ? sameDirPath
                         : File.Exists(devPath) ? devPath
                         : null;

        if (notifierPath != null)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = notifierPath,
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
            });
        }
    }
}
catch { /* Notifier is optional — don't block startup */ }

app.Run();
