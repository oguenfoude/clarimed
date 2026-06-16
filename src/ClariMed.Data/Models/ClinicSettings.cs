namespace ClariMed.Data.Models;

/// <summary>
/// Singleton clinic configuration row — auto-seeded on first run.
/// All paths are configurable to support different deployment environments.
/// </summary>
public class ClinicSettings
{
    public int Id { get; set; }
    public string ClinicName { get; set; } = "ClariMed Clinic";
    public string AETitle { get; set; } = "CLARIMED";
    public int DicomPort { get; set; } = 104;
    public string ArchivePath { get; set; } = "archive";
    public string DatabasePath { get; set; } = "db/clarimed.db";
    public int ArchiveIntervalMonths { get; set; } = 3;

    /// <summary>Folder monitored by the DocumentWatcher for incoming .docx files.</summary>
    public string WatchFolderPath { get; set; } = @"C:\ClariMed\WatchFolder";

    /// <summary>UI language: "en" or "fr".</summary>
    public string Language { get; set; } = "en";

    /// <summary>Medical report text added by the doctor, shown between cover page and images.</summary>
    public string ResumeText { get; set; } = string.Empty;

    public bool PrinterRegistered { get; set; } = false;

    /// <summary>When true, the DICOM server needs a restart to apply new port/AE Title settings.</summary>
    public bool DicomSettingsPendingRestart { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
