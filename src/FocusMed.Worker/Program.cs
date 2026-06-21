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
using FocusMed.Imaging;
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

// ── Read configuration ──
var config = builder.Configuration.GetSection("FocusMed");
var databasePath = Path.GetFullPath(config.GetValue<string>("DatabasePath") ?? "data/db/focusmed.db");
var watchFolderPath = Path.GetFullPath(config.GetValue<string>("WatchFolderPath") ?? "data/WatchFolder");
var documentOutputPath = Path.GetFullPath(config.GetValue<string>("DocumentOutputPath") ?? "data/Documents");
var archivePath = Path.GetFullPath(config.GetValue<string>("ArchivePath") ?? "data/archive");

// ── Ensure essential directories exist ──
var dataDir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
if (!string.IsNullOrEmpty(dataDir)) Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(watchFolderPath);
Directory.CreateDirectory(documentOutputPath);
Directory.CreateDirectory(archivePath);

// ── Register Application Layers ──
builder.Services.AddFocusMedData(databasePath);
builder.Services.AddFocusMedImaging();
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

// ── Authentication (Cookie-based) ──
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// ── Add Localization and Razor Pages support ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<FocusMed.Dashboard.Services.ILocalizationService, FocusMed.Dashboard.Services.LocalizationService>();
builder.Services.AddRazorPages();

var app = builder.Build();

// ── Initialize Database, License, and Seed Admin ──
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FocusMedDbContext>();
    db.Database.Migrate();

    // Ensure the Virtual Printer is registered using the correct Local Port
    var watchFolderPathStr = Path.GetFullPath(watchFolderPath);
    var printerPortPath = Path.Combine(watchFolderPathStr, "incoming_print.pdf");
    
    _ = Task.Run(() =>
    {
        try
        {
            var checkCmd = $"$p = Get-Printer -Name 'FocusMed' -ErrorAction SilentlyContinue; if ($p -and $p.PortName -eq '{printerPortPath}') {{ exit 0 }} else {{ exit 1 }}";
            var psiCheck = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{checkCmd}\"")
            {
                CreateNoWindow = true, UseShellExecute = false
            };
            var checkProc = Process.Start(psiCheck);
            checkProc?.WaitForExit();
            
            if (checkProc?.ExitCode != 0)
            {
                Console.WriteLine("[INFO] FocusMed printer not found or port mismatched. Recreating...");
                var psCmd = $"Remove-Printer -Name 'FocusMed' -ErrorAction SilentlyContinue; " +
                            $"Add-PrinterPort -Name '{printerPortPath}' -ErrorAction SilentlyContinue; " +
                            $"Add-Printer -Name 'FocusMed' -DriverName 'Microsoft Print To PDF' -PortName '{printerPortPath}'";
                
                var psiAdd = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{psCmd}\"")
                {
                    CreateNoWindow = true, UseShellExecute = true, Verb = "runas" // Request admin if needed
                };
                var addProc = Process.Start(psiAdd);
                addProc?.WaitForExit();
                Console.WriteLine("[INFO] FocusMed printer recreated successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Could not auto-create virtual printer: {ex.Message}");
        }
    });

    var settings = db.ClinicSettings.OrderBy(s => s.Id).FirstOrDefault();
    if (settings != null)
    {
        settings.PrinterRegistered = true;
        db.SaveChanges();
    }

    // Seed default admin user if no users exist
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    if (await userRepo.GetCountAsync() == 0)
    {
        await userRepo.AddAsync(new User
        {
            Username = "admin",
            DisplayName = "Administrator",
            Role = UserRole.Admin,
            IsActive = true
        }, "admin");
    }
}

// ── Bridge DI for fo-dicom ──
FellowOakDicom.DicomSetupBuilder.UseServiceProvider(app.Services);

// ── Serve Static Assets ──
app.UseStaticFiles();

// Serve DICOM images
var imagesPath = Path.GetFullPath(config.GetValue<string>("ImagesPath") ?? "data/images");
Directory.CreateDirectory(imagesPath);
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

// ── Authentication & Authorization middleware ──
app.UseAuthentication();
app.UseAuthorization();

// Redirect root to dashboard
app.MapGet("/", async context =>
{
    context.Response.Redirect("/dashboard");
    await Task.CompletedTask;
});

// Virtual Printer routes are no longer needed since we are using a local file port.

app.MapQuickAssignEndpoints();
app.MapRazorPages();

// ── Auto-open browser & Auto-start Notifier ──
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        // Only open browser automatically when running interactively (not as a Windows Service)
        if (!OperatingSystem.IsWindows() || !System.ServiceProcess.ServiceController.GetServices().Any(s => s.ServiceName == "FocusMed" && s.Status == System.ServiceProcess.ServiceControllerStatus.Running))
        {
            Process.Start(new ProcessStartInfo { FileName = "http://localhost:5000", UseShellExecute = true });
        }

        // Auto-launch Notifier: try sibling "Notifier" folder (production layout),
        // then fall back to dev Debug path.
        var baseDir = AppContext.BaseDirectory;
        var productionNotifier = Path.GetFullPath(Path.Combine(baseDir, "..", "Notifier", "FocusMed.exe"));
        var devNotifier = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "FocusMed.Notifier", "bin", "Debug", "net10.0-windows", "FocusMed.exe"));

        var notifierPath = File.Exists(productionNotifier) ? productionNotifier
                         : File.Exists(devNotifier) ? devNotifier
                         : null;

        Console.WriteLine($"[INFO] Notifier path: {notifierPath ?? "not found"}");

        if (notifierPath != null)
        {
            // Kill any existing instances first
            foreach (var proc in Process.GetProcessesByName("FocusMed"))
            {
                try { proc.Kill(); } catch { }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = notifierPath,
                WorkingDirectory = Path.GetDirectoryName(notifierPath),
                UseShellExecute = true
            });
            Console.WriteLine("[INFO] Notifier started.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARNING] Error starting processes: {ex.Message}");
    }
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        var p = Process.GetProcessesByName("FocusMed.Notifier");
        foreach (var proc in p)
        {
            try { proc.Kill(); } catch { }
        }
        Console.WriteLine("[DEBUG] Notifier processes killed on shutdown.");
    }
    catch { }
});

app.Run();
