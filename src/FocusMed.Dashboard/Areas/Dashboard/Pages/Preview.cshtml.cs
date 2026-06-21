using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusMed.Data;
using FocusMed.Data.Models;
using FocusMed.Data.Services;
using FocusMed.Printing.Cover;
using FocusMed.Printing.Merging;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Drawing;
using System.Drawing.Imaging;

namespace FocusMed.Dashboard.Areas.Dashboard.Pages;

[IgnoreAntiforgeryToken]
public class PreviewModel : PageModel
{
    private readonly FocusMedDbContext _db;
    private readonly IClinicSettingsRepository _settingsRepo;
    private readonly ICoverPageGenerator _coverGenerator;
    private readonly IPdfMerger _pdfMerger;
    private readonly IConfiguration _config;


    public Study? Study { get; set; }
    public List<DicomImage> AllImages { get; set; } = new();
    public ClinicSettings Settings { get; set; } = null!;
    public FocusMed.Data.Models.Document? LinkedDocument { get; set; }

    public PreviewModel(
        FocusMedDbContext db,
        IClinicSettingsRepository settingsRepo,
        ICoverPageGenerator coverGenerator,
        IPdfMerger pdfMerger,
        IConfiguration config)
    {
        _db = db;
        _settingsRepo = settingsRepo;
        _coverGenerator = coverGenerator;
        _pdfMerger = pdfMerger;
        _config = config;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Study = await _db.Studies
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Patient)
            .Include(s => s.SeriesList)
                .ThenInclude(ser => ser.Images)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (Study == null)
            return RedirectToPage("/Patients");

        Settings = await _settingsRepo.GetAsync();

        AllImages = Study.SeriesList
            .OrderBy(s => s.SeriesNumber)
            .SelectMany(s => s.Images)
            .OrderBy(i => i.InstanceNumber)
            .ToList();

        LinkedDocument = await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.StudyId == id && d.Status == DocumentStatus.Converted);

        return Page();
    }

    public class GeneratePdfRequest
    {
        public int StudyId { get; set; }
        public List<string> OrderedImagePaths { get; set; } = new();
        public int ColumnsPerRow { get; set; }
        public int ImagesPerPage { get; set; }
        public int GapPx { get; set; }
        public PrintFormat Format { get; set; } = PrintFormat.A4;
    }

    public async Task<IActionResult> OnPostGeneratePdfAsync([FromBody] GeneratePdfRequest request)
    {
        if (request == null || request.OrderedImagePaths.Count == 0)
            return BadRequest("Invalid request");

        var study = await _db.Studies
            .AsNoTracking()
            .Include(s => s.Patient)
            .FirstOrDefaultAsync(s => s.Id == request.StudyId);

        if (study == null)
            return NotFound("Study not found");

        var settings = await _settingsRepo.GetAsync();

        var document = await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.StudyId == request.StudyId && d.Status == DocumentStatus.Converted);
        string? actualReportPdfPath = document?.PdfFilePath;

        var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documents", "Temp");
        Directory.CreateDirectory(outputDir);

        // 1. Cover
        var context = new PrintJobContext(
            ClinicName: settings.ClinicName,
            PatientName: study.Patient?.Name ?? "Unknown",
            PatientId: study.Patient?.PatientId ?? "Unknown",
            StudyDescription: study.StudyDescription ?? "Medical Imaging Report",
            Modality: study.Modality,
            StudyDate: study.StudyDate,
            ReferringPhysician: study.ReferringPhysicianName,
            InstitutionName: study.InstitutionName,
            PatientSex: study.Patient?.Sex,
            PatientBirthDate: study.Patient?.BirthDate,
            AccessionNumber: study.AccessionNumber
        );

        string coverPdfPath = await _coverGenerator.GenerateAsync(context, outputDir, CancellationToken.None);

        // 2. Image Plates
        QuestPDF.Settings.License = LicenseType.Community;
        string reportPdfPath = Path.Combine(outputDir, $"plates_{Guid.NewGuid()}.pdf");

        var imagesDir = Path.GetFullPath(_config["FocusMed:ImagesPath"] ?? Path.Combine("data", "images"));
        var absPaths = new List<string>();
        foreach (var p in request.OrderedImagePaths)
        {
            var rel = p.Replace("/dicom-images/", "").Replace("/", "\\");
            var abs = Path.Combine(imagesDir, rel);
            if (System.IO.File.Exists(abs)) absPaths.Add(abs);
        }

        QuestPDF.Fluent.Document.Create(container =>
        {
            for (int i = 0; i < absPaths.Count; i += request.ImagesPerPage)
            {
                var batch = absPaths.Skip(i).Take(request.ImagesPerPage).ToList();
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.PageColor(Colors.White);

                    int rows = (int)Math.Ceiling(request.ImagesPerPage / (double)request.ColumnsPerRow);
                    page.Content().Padding(request.GapPx).Column(col =>
                    {
                        col.Spacing(request.GapPx);
                        
                        for (int r = 0; r < rows; r++)
                        {
                            var rowBatch = batch.Skip(r * request.ColumnsPerRow).Take(request.ColumnsPerRow).ToList();
                            col.Item().Row(row =>
                            {
                                row.Spacing(request.GapPx);
                                foreach (var imgPath in rowBatch)
                                {
                                    byte[] optimizedBytes = OptimizeImageForPdf(imgPath);
                                    row.RelativeItem().Image(optimizedBytes).FitWidth();
                                }
                                for (int pad = rowBatch.Count; pad < request.ColumnsPerRow; pad++)
                                {
                                    row.RelativeItem();
                                }
                            });
                        }
                    });
                });
            }
        }).GeneratePdf(reportPdfPath);

        // 3. Merge
        string finalOutputDir = Path.GetFullPath(_config["FocusMed:MergedPdfOutputPath"] ?? Path.Combine("data", "output"));
        Directory.CreateDirectory(finalOutputDir);
        string finalOutputPath = Path.Combine(finalOutputDir, $"Merged_{study.AccessionNumber}_{Guid.NewGuid()}.pdf");
        await _pdfMerger.MergeAsync(coverPdfPath, actualReportPdfPath, new[] { reportPdfPath }, Array.Empty<string>(), finalOutputPath, request.Format, request.ImagesPerPage, request.ColumnsPerRow, request.GapPx, CancellationToken.None);

        // Cleanup
        try { System.IO.File.Delete(coverPdfPath); } catch { }
        try { System.IO.File.Delete(reportPdfPath); } catch { }

        return new JsonResult(new { success = true, downloadUrl = $"/preview/{study.Id}?handler=DownloadPdf&file={Path.GetFileName(finalOutputPath)}" });
    }

    public IActionResult OnGetDownloadPdf(string file)
    {
        string outputDir = Path.GetFullPath(_config["FocusMed:MergedPdfOutputPath"] ?? Path.Combine("data", "output"));
        var path = Path.Combine(outputDir, file);
        if (!System.IO.File.Exists(path)) return NotFound();
        // Return inline to display in the iframe, DO NOT add the fileDownloadName parameter
        return PhysicalFile(path, "application/pdf");
    }

    public IActionResult OnGetThumbnail(string path, int size = 200)
    {
        if (string.IsNullOrEmpty(path)) return NotFound();
        
        var imagesDir = Path.GetFullPath(_config["FocusMed:ImagesPath"] ?? Path.Combine("data", "images"));
        var rel = path.Replace("/dicom-images/", "").Replace("/", "\\").TrimStart('\\');
        var absPath = Path.Combine(imagesDir, rel);

        if (!System.IO.File.Exists(absPath))
            return NotFound();

        byte[] thumbnailBytes = GenerateThumbnailBytes(absPath, size);
        return File(thumbnailBytes, "image/jpeg");
    }

    public async Task<IActionResult> OnGetCoverPdfAsync(int id)
    {
        var study = await _db.Studies
            .AsNoTracking()
            .Include(s => s.Patient)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (study == null) return NotFound();
        
        var settings = await _settingsRepo.GetAsync();
        var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documents", "Temp");
        Directory.CreateDirectory(outputDir);

        var context = new PrintJobContext(
            ClinicName: settings.ClinicName,
            PatientName: study.Patient?.Name ?? "Unknown",
            PatientId: study.Patient?.PatientId ?? "Unknown",
            StudyDescription: study.StudyDescription ?? "Medical Imaging Report",
            Modality: study.Modality,
            StudyDate: study.StudyDate,
            ReferringPhysician: study.ReferringPhysicianName,
            InstitutionName: study.InstitutionName,
            PatientSex: study.Patient?.Sex,
            PatientBirthDate: study.Patient?.BirthDate,
            AccessionNumber: study.AccessionNumber
        );

        string coverPdfPath = await _coverGenerator.GenerateAsync(context, outputDir, CancellationToken.None);
        var bytes = await System.IO.File.ReadAllBytesAsync(coverPdfPath);
        try { System.IO.File.Delete(coverPdfPath); } catch { }

        return new JsonResult(new { base64 = Convert.ToBase64String(bytes) });
    }

    public async Task<IActionResult> OnGetReportPdfAsync(int docId)
    {
        var doc = await _db.Documents.FindAsync(docId);
        if (doc == null || string.IsNullOrEmpty(doc.PdfFilePath) || !System.IO.File.Exists(doc.PdfFilePath))
            return NotFound();
        return PhysicalFile(Path.GetFullPath(doc.PdfFilePath), "application/pdf");
    }

    public async Task<IActionResult> OnPostRemoveDocumentAsync(int studyId, int docId)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == docId && d.StudyId == studyId);
        if (doc != null)
        {
            doc.StudyId = null;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id = studyId });
    }

    public async Task<IActionResult> OnGetCheckStatusAsync(int id)
    {
        var study = await _db.Studies.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (study == null) return NotFound();

        if (study.Status == StudyStatus.Receiving)
        {
            return StatusCode(200); // 200 OK, empty content, keeps HTMX polling
        }
        
        // Complete! Redirect the client back to this page to remove overlay and show images.
        Response.Headers["HX-Redirect"] = $"/preview/{id}";
        return StatusCode(200);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var study = await _db.Studies.FirstOrDefaultAsync(s => s.Id == id);
        if (study != null)
        {
            study.IsDeleted = true;
            study.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage("/Patients");
    }

    private byte[] OptimizeImageForPdf(string imagePath)
    {
        return GenerateThumbnailBytes(imagePath, 800);
    }

    private byte[] GenerateThumbnailBytes(string imagePath, int maxSize)
    {
        try
        {
            using var originalImage = System.Drawing.Image.FromFile(imagePath);

            if (originalImage.Width <= maxSize && originalImage.Height <= maxSize)
            {
                using var ms = new MemoryStream();
                SaveAsJpeg(originalImage, ms);
                return ms.ToArray();
            }

            var ratioX = (double)maxSize / originalImage.Width;
            var ratioY = (double)maxSize / originalImage.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(originalImage.Width * ratio);
            var newHeight = (int)(originalImage.Height * ratio);

            using var newImage = new Bitmap(newWidth, newHeight);
            using var graphics = Graphics.FromImage(newImage);
            
            // Fill background with white to prevent transparent issues when converting to JPEG
            graphics.Clear(System.Drawing.Color.White);

            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
            graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);

            using var msOut = new MemoryStream();
            SaveAsJpeg(newImage, msOut);
            return msOut.ToArray();
        }
        catch
        {
            // Fallback if anything goes wrong
            if (System.IO.File.Exists(imagePath))
                return System.IO.File.ReadAllBytes(imagePath);
            return Array.Empty<byte>();
        }
    }

    private void SaveAsJpeg(System.Drawing.Image img, MemoryStream ms)
    {
        var jpegEncoder = ImageCodecInfo.GetImageDecoders().FirstOrDefault(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
        if (jpegEncoder != null)
        {
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
            img.Save(ms, jpegEncoder, encoderParams);
        }
        else
        {
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        }
    }
}
