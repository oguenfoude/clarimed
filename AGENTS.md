# ClariMed — AGENTS.md

## Build & Run

- **Solution**: `ClariMed.slnx` (XML-based, not `.sln`) — tools expecting `.sln` will fail.
- **Target**: `net10.0-windows` — Windows-only.
- **EF Core**: pinned to `8.0.0` despite `net10.0`. Migrations from `ClariMed.Data`:
  ```
  dotnet ef migrations add <Name> --project src\ClariMed.Data --startup-project src\ClariMed.Worker
  ```
  Design-time factory uses `Data Source=db/clarimed.db`.
- **Run**: `dotnet watch run --project src\ClariMed.Worker` — uses `launchSettings.json`, opens `http://localhost:5000`.
- **No tests** — `tests/` dir is empty. No CI/CD.

## Architecture

- **Worker** (`ClariMed.Worker`) is the sole orchestrator. 6 background services:
  `DicomListenerService`, `DocumentProcessingService`, `DocumentWatcherService`, `PrintJobProcessor`, `StudyCompletionService`, `VirtualPrinterService`.
  All registered in `Program.cs`.
- **8 projects**: each library exposes `ServiceCollectionExtensions` with `AddClariMedXxx()`.
- **Dependency flow**: Worker → everything else. Libraries only depend on `ClariMed.Data` (except `ClariMed.Printing` and `ClariMed.Imaging` which are standalone).
- **Dashboard**: Razor Pages Areas pattern under `Areas/Dashboard/Pages/`. Served by Worker process.

## Database

- **SQLite WAL mode** via `WalModeInterceptor` (singleton on every connection). PRAGMAs: `journal_mode=WAL`, `busy_timeout=5000`, `synchronous=NORMAL`, `cache_size=-64000`, `foreign_keys=ON`.
- **DB row takes precedence** over `appsettings.json` for most settings (loaded at service start). Exception: `StudyStabilizationSeconds` is config-only.
- `ClinicSettingsRepository.GetAsync()` auto-seeds a default row if none exists.
- `db.Database.Migrate()` runs on every startup in `Program.cs`.

## Key Conventions & Pitfalls

- **NuGet audit advisories suppressed** in `Directory.Build.props` for SixLabors.ImageSharp transitive from PdfSharpCore 1.x. **Do not upgrade** ImageSharp — PdfSharpCore uses the 1.x API surface. Blocked on replacing PdfSharpCore with PdfSharp 6+.
- **PdfiumViewer** targets .NET Framework; `NU1701` suppressed in its `.csproj`. Do not attempt to "fix" this.
- **FreeSpire.Doc** free version: 500 paragraph / 3 page limit per conversion. OK for cover sheets, not for long reports.
- **QuestPDF** Community license: revenue < $1M/year. Set in `Program.cs`.
- **DICOM C-STORE** on port 104 (requires Admin or `netsh urlacl`). Failure to bind logs a warning but does not crash the host — other services continue.
- **Virtual Printer** on port 5000 via IPP endpoint at `/printers/clarimed`. Windows printer registration runs PowerShell commands requiring Admin. Failure is non-fatal (logged as warning).
- **`DicomFileIngestionService`** uses a `SemaphoreSlim(1,1)` for sequential DB upserts — single-threaded ingestion by design.

## DICOM / Test Files

- Test DCM files are at `C:\Users\Administrator\Downloads\*.dcm`.
- To ingest: drop into `D:\ClariMed\dcm\` dir, then use the Dashboard button, or send via DICOM C-STORE to port 104, or copy files and restart the app (auto-ingestion on startup is not implemented — no `--ingest` handler in `Program.cs`).
- Duplicate prevention: `SopInstanceUid` unique index on `DicomImage`.

## File System (Runtime)

```
C:\ClariMed\WatchFolder\   — drop .docx here
C:\ClariMed\Documents\     — converted PDFs
C:\ClariMed\Output\        — merged PDFs

D:\ClariMed\db\clarimed.db — SQLite database
D:\ClariMed\data\archive\  — raw .dcm files
D:\ClariMed\data\images\   — converted .png files
D:\ClariMed\templates\     — pagegarde.docx cover template
```
