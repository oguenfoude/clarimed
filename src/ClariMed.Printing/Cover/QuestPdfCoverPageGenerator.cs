using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClariMed.Printing.Cover;

/// <summary>
/// Generates a cover page ("Page de Garde") for a print job.
/// Primary mode: copies the static PDF template configured in
/// <c>ClariMed:CoverPageTemplatePath</c> (default: <c>templates/pagegarde.pdf</c>).
/// Fallback: if the template file is missing, generates a minimal QuestPDF cover
/// so the print pipeline never crashes.
/// </summary>
public class QuestPdfCoverPageGenerator : ICoverPageGenerator
{
    private readonly ILogger<QuestPdfCoverPageGenerator> _logger;
    private readonly string _templatePath;

    public QuestPdfCoverPageGenerator(
        ILogger<QuestPdfCoverPageGenerator> logger,
        IConfiguration configuration,
        Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        _logger = logger;
        var configured = configuration["ClariMed:CoverPageTemplatePath"] ?? "templates/pagegarde.pdf";
        if (!Path.IsPathRooted(configured))
        {
            configured = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", configured));
        }
        _templatePath = Path.GetFullPath(configured);
    }

    public Task<string> GenerateAsync(
        PrintJobContext ctx, string outputDir, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(outputDir);
            var filePath = Path.Combine(outputDir, $"Cover_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            // ── Primary: use Word template ──
            if (File.Exists(_templatePath) && _templatePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                var document = new Spire.Doc.Document();
                document.LoadFromFile(_templatePath);
                
                // Replace placeholders
                document.Replace("{{PatientName}}", ctx.PatientName.ToUpper(), false, true);
                var dateStr = ctx.StudyDate?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy");
                document.Replace("{{StudyDate}}", dateStr, false, true);
                
                // Convert to PDF
                document.SaveToFile(filePath, Spire.Doc.FileFormat.PDF);
                
                _logger.LogInformation("Cover page generated from Word template: {Template} → {Output}", _templatePath, filePath);
                return filePath;
            }

            // ── Fallback: generate a minimal cover via QuestPDF ──
            _logger.LogWarning(
                "Cover page template not found at {Path} — generating fallback cover.",
                _templatePath);

            // ── Generate Cover via QuestPDF ──
            _logger.LogInformation("Generating native PDF cover page for {PatientName}", ctx.PatientName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(40);
                    page.MarginVertical(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item()
                            .BorderBottom(2)
                            .BorderColor(Colors.Blue.Darken3)
                            .PaddingBottom(8)
                            .Text(ctx.ClinicName)
                            .FontSize(22)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);

                        col.Item()
                            .PaddingTop(4)
                            .Text("Rapport Médical — Page de Garde")
                            .FontSize(13)
                            .FontColor(Colors.Grey.Darken1);
                    });

                    // Content
                    page.Content().PaddingTop(20).Column(col =>
                    {
                        col.Item().PaddingBottom(10).Text("INFORMATIONS DU PATIENT")
                            .FontSize(13).Bold().FontColor(Colors.Blue.Darken2);

                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(inner =>
                        {
                            InfoRow(inner, "Nom du Patient", ctx.PatientName);
                            InfoRow(inner, "ID Patient", ctx.PatientId);
                            if (ctx.PatientSex != null)
                                InfoRow(inner, "Sexe", ctx.PatientSex);
                            if (ctx.PatientBirthDate.HasValue)
                                InfoRow(inner, "Date de Naissance", ctx.PatientBirthDate.Value.ToString("dd/MM/yyyy"));
                        });

                        col.Item().Height(20);

                        col.Item().PaddingBottom(10).Text("INFORMATIONS DE L'EXAMEN")
                            .FontSize(13).Bold().FontColor(Colors.Blue.Darken2);

                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(inner =>
                        {
                            InfoRow(inner, "Description", ctx.StudyDescription);
                            InfoRow(inner, "Modalité", ctx.Modality);
                            InfoRow(inner, "Date de l'Examen",
                                ctx.StudyDate?.ToString("dd/MM/yyyy") ?? "N/A");
                            if (!string.IsNullOrWhiteSpace(ctx.AccessionNumber))
                                InfoRow(inner, "Numéro d'Accession", ctx.AccessionNumber);
                        });
                    });

                    // Footer
                    page.Footer()
                        .BorderTop(1)
                        .BorderColor(Colors.Grey.Lighten1)
                        .PaddingTop(6)
                        .Row(row =>
                        {
                            row.RelativeItem().Text($"Généré par ClariMed — {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(8).FontColor(Colors.Grey.Medium);

                            row.ConstantItem(80).AlignRight()
                                .Text("Page 1").FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                });
            })
            .GeneratePdf(filePath);

            _logger.LogInformation("Generated fallback cover page: {Path}", filePath);
            return filePath;
        }, cancellationToken);
    }

    /// <summary>
    /// Renders a single label-value row inside a section block.
    /// </summary>
    private static void InfoRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(4).Row(row =>
        {
            row.ConstantItem(160)
                .Text(label + ":")
                .Bold()
                .FontSize(10)
                .FontColor(Colors.Grey.Darken2);

            row.RelativeItem()
                .Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(10);
        });
    }
}
