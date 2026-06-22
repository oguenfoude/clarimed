# AGENTS.md — FocusMed

## Quick Facts

- **.NET 10.0** (`net10.0-windows`) — Windows-only (printing, WinForms, Windows Service)
- **Solution file**: `FocusMed.slnx` (XML format, NOT `.sln` — tools expecting `.sln` will fail)
- **No tests, no CI/CD, no linter, no `.editorconfig`**
- **Default credentials**: `admin` / `admin` (seeded on first run)
- **Dashboard URL**: `http://localhost:5000`

## Build & Run

```bash
# Build
dotnet build FocusMed.slnx

# Run (console mode, Ctrl+C to stop)
dotnet run --project src/FocusMed.Worker

# Run with hot-reload
dotnet watch run --project src/FocusMed.Worker

# Release build (packages installer)
.\build_release.ps1
```

### EF Core Migrations

```bash
# Add migration
dotnet ef migrations add <Name> --project src/FocusMed.Data --startup-project src/FocusMed.Worker

# Apply (also runs automatically on startup via db.Database.Migrate())
dotnet ef database update --project src/FocusMed.Data --startup-project src/FocusMed.Worker
```

## Architecture

Each layer has a `ServiceCollectionExtensions.cs` registering DI. Entry point is `Program.cs` in FocusMed.Worker.

```
FocusMed.Data         → EF Core 8 + SQLite (WAL mode), repository pattern (scoped)
FocusMed.Dicom        → fo-dicom C-STORE SCP on TCP:1004 + DicomUpsertService
FocusMed.Documents    → .docx watch folder → FreeSpire PDF conversion (producer-consumer via Channels)
FocusMed.Printing     → QuestPDF (cover) + PdfSharpCore (grid layout + A3/booklet imposition) + PdfiumViewer (silent print)
FocusMed.Dashboard    → Razor Pages (Areas pattern) + HTMX + Tailwind + Alpine.js
FocusMed.Worker       → Windows Service host, orchestrates all background services
FocusMed.Notifier     → Standalone WinForms tray app (update checker, quick-assign, auto-started by Worker)
DicomSender           → Standalone test utility (NOT in solution file)
FocusMed.Installer    → Self-extracting installer (NOT in solution file, requires Admin)
```

### Project Reference Graph

```
Worker → Dicom, Printing, Data, Documents, Dashboard
Dashboard → Data, Printing
Dicom → Data, Printing
Documents → Data
Notifier → Data
```

### Background Services (7)

All registered as `IHostedService` in `Program.cs`:

| Service | Purpose |
|---------|---------|
| `DicomListenerService` | TCP:1004 DICOM server, restarts on settings change |
| `DocumentWatcherService` | Starts FileSystemWatcher on watch folder |
| `DocumentProcessingService` | Drains document queue, converts .docx → PDF |
| `StudyCompletionService` | Polls every 10s, marks stale studies Complete |
| `UpdateCheckerService` | Polls GitHub for new releases |
| `RecycleBinCleanupService` | Deletes soft-deleted studies > 30 days (every 12h) |
| `DicomRestartService` | Polls every 5s, restarts DICOM server when settings change |

## Configuration Precedence

`appsettings.json` → **DB `ClinicSettings` table wins** at runtime for most settings.

Data paths are resolved relative to `C:\ProgramData\FocusMed` (via `GetSafeDataPath` in Program.cs). Development mode uses relative paths via `appsettings.Development.json`.

## Key Gotchas

- **`pdfium.dll` locking** — `UseAppHost=false` + `Directory.Build.targets` workaround prevents DLL lock during `dotnet watch`. Don't remove these.
- **FreeSpire.Doc free tier** — 3-page / 500-paragraph limit for .docx → PDF conversion.
- **DICOM auto-restart** — Changing AE Title/port sets a flag; `DicomRestartService` waits for idle (no `Receiving` studies) before restarting. Dashboard polls status every 3s.
- **NuGet audit suppressions** — `Directory.Build.props` suppresses advisories for `SixLabors.ImageSharp` (transitive from PdfSharpCore) and `SQLitePCLRaw`. Don't remove without understanding why.
- **FocusMed.Installer requires Admin** — `app.manifest` has `requireAdministrator`.
- **FocusMed.Notifier is single-instance** — Uses Mutex; Worker kills and restarts it during backend restarts.
- **Print profiles** — Paper size, tray, and booklet finishing are controlled by Windows printer queue defaults, not by application code. The app only selects the printer by name.
- **Image path resolution** — `DicomImage.FilePath` stores the absolute `.dcm` path. To get `.png` path: replace `archive` → `images` and `.dcm` → `.png`. The `GetRelative()` helper in Preview.cshtml strips to a relative path for the frontend.
- **PdfSharpMerger grid layout** — Images are laid out in a grid on A4 pages (imagesPerPage × columnsPerRow with gap), then format-transformed. A4 = as-is, A3 = scaled to A3 portrait, Booklet = A4 pairs side-by-side on A3 landscape sheets.
- **PDF margin = 2 × gapPx** — Preview has double padding (outer div `padding: gap` + inner grid `padding: gap`), so PDF uses `margin = 2 * gap` to match exactly.
- **Per-study DICOM locking** — `ConcurrentDictionary<string, SemaphoreSlim>` allows parallel ingestion of different studies while serializing images within the same study. Don't revert to a global lock.
- **ClinicSettings caching** — `ClinicSettingsRepository` caches settings for 30 seconds with invalidation on `UpdateAsync()`. This eliminates a DB hit on every HTTP request.
- **No sync-over-async** — `LocalizationService` reads language from `HttpContext.Items["CurrentLanguage"]` (set by middleware). Never use `.GetAwaiter().GetResult()` in scoped services.
- **Read-only queries** — All read-only queries use `AsNoTracking()`. Only write operations use tracked entities.

## i18n

Custom `ILocalizationService` with JSON files (`Resources/en.json`, `Resources/fr.json`). Not built-in .NET localization. Tag helper: `@Loc["key"]`. Language stored in `ClinicSettings.Language`.

## Testing

No test framework configured. `tests/` directory is a placeholder. `DicomSender` is a standalone console app for manual DICOM testing (sends .dcm files to localhost:1004).

## Style

- No analyzers, no formatter, no `.editorconfig`
- Razor Pages use Areas pattern: `Areas/Dashboard/Pages/`
- Each page lives in its own subfolder with `Index.cshtml`/`Index.cshtml.cs`. Only `_Layout.cshtml` stays in `Shared/`
- Settings page is HTMX-driven with partial views (`_SettingsGeneral.cshtml`, etc.)
- All dashboard pages require `[Authorize]` (set globally in `_ViewImports.cshtml`)
