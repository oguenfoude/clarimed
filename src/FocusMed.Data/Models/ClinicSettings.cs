namespace FocusMed.Data.Models;

/// <summary>
/// Singleton clinic configuration row — auto-seeded on first run.
/// All paths are configurable to support different deployment environments.
/// </summary>
public class ClinicSettings
{
    public int Id { get; set; }
    public string ClinicName { get; set; } = "FocusMed Clinic";
    public string AETitle { get; set; } = "FOCUSMED";
    public int DicomPort { get; set; } = 1004;
    public string ArchivePath { get; set; } = "archive";
    public string DatabasePath { get; set; } = "db/focusmed.db";
    public int ArchiveIntervalMonths { get; set; } = 3;

    /// <summary>Folder monitored by the DocumentWatcher for incoming .docx files.</summary>
    public string WatchFolderPath { get; set; } = "data/WatchFolder";

    /// <summary>UI language: "en" or "fr".</summary>
    public string Language { get; set; } = "en";

    /// <summary>Medical report text added by the doctor, shown between cover page and images.</summary>
    public string ResumeText { get; set; } = string.Empty;

    /// <summary>Windows printer queue name for A3 printing.</summary>
    public string PrinterA3 { get; set; } = string.Empty;

    /// <summary>Windows printer queue name for A4 printing.</summary>
    public string PrinterA4 { get; set; } = string.Empty;

    /// <summary>Windows printer queue name for booklet/saddle-stitch printing.</summary>
    public string PrinterBooklet { get; set; } = string.Empty;

    public bool DicomSettingsPendingRestart { get; set; } = false;

    public bool UpdateAvailable { get; set; } = false;
    public string LatestVersion { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
