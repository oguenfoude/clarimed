using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FocusMed.Data;
using FocusMed.Data.Models;
using FocusMed.Printing.Cover;
using FocusMed.Printing.Merging;
using FocusMed.Printing.SilentPrint;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FocusMed.Dashboard.Api;

public static class PrintEndpoints
{
    public class PrintJobRequest
    {
        public string[] OrderedImagePaths { get; set; } = Array.Empty<string>();
        public int ImagesPerPage { get; set; } = 8;
        public int ColumnsPerRow { get; set; } = 2;
        public int GapPx { get; set; } = 2;
    }

    public static void MapPrintEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/print");

        group.MapPost("/silent/{studyId:int}", async (
            int studyId,
            [FromQuery] PrintFormat printFormat,
            [FromBody] PrintJobRequest? body,
            FocusMedDbContext db,
            ICoverPageGenerator coverGenerator,
            IPdfMerger pdfMerger,
            ISilentPrinter silentPrinter,
            IConfiguration config,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PrintEndpoints");

            var study = await db.Studies
                .Include(s => s.Patient)
                .Include(s => s.SeriesList)
                    .ThenInclude(s => s.Images.OrderBy(i => i.InstanceNumber))
                .FirstOrDefaultAsync(s => s.Id == studyId && !s.IsDeleted);

            if (study == null) return Results.NotFound("Study not found.");

            var settings = await db.ClinicSettings.FirstOrDefaultAsync();
            var targetPrinterName = settings?.PrinterA4;

            if (string.IsNullOrWhiteSpace(targetPrinterName))
                return Results.BadRequest("No printer has been selected in Settings.");

            try
            {
                var tempDir = Path.Combine(AppContext.BaseDirectory, "Documents", "Temp");
                Directory.CreateDirectory(tempDir);

                var coverCtx = new PrintJobContext(
                    settings?.ClinicName ?? "FocusMed Clinic",
                    study.Patient.Name,
                    study.Patient.PatientId,
                    study.StudyDescription,
                    study.Modality,
                    study.StudyDate ?? study.CreatedAt,
                    study.ReferringPhysicianName,
                    study.InstitutionName,
                    study.Patient.Sex,
                    study.Patient.BirthDate,
                    study.AccessionNumber
                );
                var coverPdfPath = await coverGenerator.GenerateAsync(coverCtx, tempDir, CancellationToken.None);

                var doc = await db.Documents
                    .Where(d => d.StudyId == study.Id && d.Status == DocumentStatus.Converted)
                    .OrderByDescending(d => d.ReceivedAt)
                    .FirstOrDefaultAsync();
                var reportPdfPath = doc?.PdfFilePath;

                var pngPaths = ResolveImagePaths(body?.OrderedImagePaths, study, config);

                var finalPdfPath = Path.Combine(tempDir, $"Print_{studyId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

                var ipp = body?.ImagesPerPage > 0 ? body.ImagesPerPage : 8;
                var cols = body?.ColumnsPerRow > 0 ? body.ColumnsPerRow : 2;
                var gap = Math.Max(0, body?.GapPx ?? 2);

                await pdfMerger.MergeAsync(
                    coverPdfPath, reportPdfPath, new string[0],
                    pngPaths, finalPdfPath, printFormat, ipp, cols, gap, CancellationToken.None);

                await silentPrinter.PrintPdfAsync(finalPdfPath, targetPrinterName, printFormat, CancellationToken.None);

                return Results.Ok(new { success = true, message = "Sent to printer." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Silent print failed for study {StudyId}", studyId);
                return Results.Problem(ex.Message);
            }
        });

        group.MapPost("/download/{studyId:int}", async (
            int studyId,
            [FromQuery] PrintFormat printFormat,
            [FromBody] PrintJobRequest? body,
            FocusMedDbContext db,
            ICoverPageGenerator coverGenerator,
            IPdfMerger pdfMerger,
            IConfiguration config,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PrintEndpoints");

            var study = await db.Studies
                .Include(s => s.Patient)
                .Include(s => s.SeriesList)
                    .ThenInclude(s => s.Images.OrderBy(i => i.InstanceNumber))
                .FirstOrDefaultAsync(s => s.Id == studyId && !s.IsDeleted);

            if (study == null) return Results.NotFound("Study not found.");

            try
            {
                var tempDir = Path.Combine(AppContext.BaseDirectory, "Documents", "Temp");
                Directory.CreateDirectory(tempDir);

                var settings = await db.ClinicSettings.FirstOrDefaultAsync();

                var coverCtx = new PrintJobContext(
                    settings?.ClinicName ?? "FocusMed Clinic",
                    study.Patient.Name,
                    study.Patient.PatientId,
                    study.StudyDescription,
                    study.Modality,
                    study.StudyDate ?? study.CreatedAt,
                    study.ReferringPhysicianName,
                    study.InstitutionName,
                    study.Patient.Sex,
                    study.Patient.BirthDate,
                    study.AccessionNumber
                );
                var coverPdfPath = await coverGenerator.GenerateAsync(coverCtx, tempDir, CancellationToken.None);

                var doc = await db.Documents
                    .Where(d => d.StudyId == study.Id && d.Status == DocumentStatus.Converted)
                    .OrderByDescending(d => d.ReceivedAt)
                    .FirstOrDefaultAsync();
                var reportPdfPath = doc?.PdfFilePath;

                var pngPaths = ResolveImagePaths(body?.OrderedImagePaths, study, config);

                var finalPdfPath = Path.Combine(tempDir, $"Download_{studyId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

                var ipp = body?.ImagesPerPage > 0 ? body.ImagesPerPage : 8;
                var cols = body?.ColumnsPerRow > 0 ? body.ColumnsPerRow : 2;
                var gap = Math.Max(0, body?.GapPx ?? 2);

                await pdfMerger.MergeAsync(
                    coverPdfPath, reportPdfPath, new string[0],
                    pngPaths, finalPdfPath, printFormat, ipp, cols, gap, CancellationToken.None);

                var bytes = await System.IO.File.ReadAllBytesAsync(finalPdfPath);
                return Results.File(bytes, "application/pdf", $"Study_{studyId}.pdf");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Download failed for study {StudyId}", studyId);
                return Results.Problem(ex.Message);
            }
        });

        group.MapPost("/silent-existing/{studyId:int}", async (
            int studyId,
            string file,
            [FromQuery] PrintFormat printFormat,
            FocusMedDbContext db,
            ISilentPrinter silentPrinter,
            IConfiguration config,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PrintEndpoints");

            if (string.IsNullOrWhiteSpace(file) || file.Contains("/") || file.Contains("\\"))
                return Results.BadRequest("Invalid file parameter.");

            var settings = await db.ClinicSettings.FirstOrDefaultAsync();
            var targetPrinterName = settings?.PrinterA4;

            if (string.IsNullOrWhiteSpace(targetPrinterName))
                return Results.BadRequest("No printer has been selected in Settings.");

            string outputDir = Path.GetFullPath(config["FocusMed:MergedPdfOutputPath"] ?? Path.Combine("data", "output"));
            var finalPdfPath = Path.Combine(outputDir, file);

            if (!System.IO.File.Exists(finalPdfPath))
                return Results.NotFound("The generated PDF file could not be found.");

            try
            {
                await silentPrinter.PrintPdfAsync(finalPdfPath, targetPrinterName, printFormat, CancellationToken.None);
                return Results.Ok(new { success = true, message = "Sent to printer." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Silent print existing failed for study {StudyId}", studyId);
                return Results.Problem(ex.Message);
            }
        });
    }

    private static string[] ResolveImagePaths(string[]? orderedPaths, Study study, IConfiguration config)
    {
        if (orderedPaths != null && orderedPaths.Length > 0)
        {
            var imagesDir = Path.GetFullPath(config["FocusMed:ImagesPath"] ?? Path.Combine("data", "images"));
            return orderedPaths
                .Select(p =>
                {
                    var rel = p.Replace("/dicom-images/", "").Replace("/", "\\");
                    return Path.Combine(imagesDir, rel);
                })
                .ToArray();
        }

        return study.SeriesList
            .OrderBy(s => s.SeriesNumber)
            .SelectMany(s => s.Images)
            .OrderBy(i => i.InstanceNumber)
            .Select(i => i.FilePath
                .Replace("archive", "images")
                .Replace(".dcm", ".png"))
            .ToArray();
    }
}
