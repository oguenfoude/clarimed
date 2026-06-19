using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ClariMed.Data;
using ClariMed.Data.Models;
using ClariMed.Data.Services;
using ClariMed.Dicom;
using ClariMed.Documents;
using ClariMed.Imaging;
using ClariMed.Printing;
using ClariMed.Worker.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

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
    options.ServiceName = "ClariMed";
});

// ── Read configuration ──
var config = builder.Configuration.GetSection("ClariMed");
var databasePath = config.GetValue<string>("DatabasePath") ?? "db/clarimed.db";
var watchFolderPath = config.GetValue<string>("WatchFolderPath") ?? @"C:\ClariMed\WatchFolder";
var documentOutputPath = config.GetValue<string>("DocumentOutputPath") ?? @"C:\ClariMed\Documents";
var archivePath = config.GetValue<string>("ArchivePath") ?? "data/archive";

// ── Ensure essential directories exist ──
var dataDir = Path.GetDirectoryName(Path.GetFullPath(databasePath));
if (!string.IsNullOrEmpty(dataDir)) Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(watchFolderPath);
Directory.CreateDirectory(documentOutputPath);
Directory.CreateDirectory(archivePath);

// ── Register Application Layers ──
builder.Services.AddClariMedData(databasePath);
builder.Services.AddClariMedImaging();
builder.Services.AddClariMedDicom();
builder.Services.AddClariMedDocuments();
builder.Services.AddClariMedPrinting();

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
builder.Services.AddSingleton<ClariMed.Dashboard.Services.ILocalizationService, ClariMed.Dashboard.Services.LocalizationService>();
builder.Services.AddRazorPages();

var app = builder.Build();

// ── Initialize Database, License, and Seed Admin ──
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();
    db.Database.Migrate();

    // Reset PrinterRegistered flag once on startup to force migration to port 5000
    var settings = db.ClinicSettings.OrderBy(s => s.Id).FirstOrDefault();
    if (settings != null)
    {
        settings.PrinterRegistered = false;
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
var imagesPath = Path.GetFullPath("data/images");
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

app.MapRazorPages();

// ── Auto-open browser ──
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        Process.Start(new ProcessStartInfo { FileName = "http://localhost:5000", UseShellExecute = true });
    }
    catch { /* Best effort */ }
});

app.Run();
