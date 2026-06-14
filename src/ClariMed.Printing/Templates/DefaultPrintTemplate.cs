using System.Drawing;
using System.Drawing.Drawing2D;

namespace ClariMed.Printing.Templates;

/// <summary>
/// Default medical report template layout:
///   ┌─────────────────────────────┐
///   │       CLINIC NAME           │  ← header
///   │─────────────────────────────│
///   │  Patient: ...    ID: ...    │  ← patient info
///   │  Study: ...      Date: ... │
///   │  Modality: ...              │
///   │─────────────────────────────│
///   │                             │
///   │        [ DICOM IMAGE ]      │  ← centered image
///   │                             │
///   │─────────────────────────────│
///   │  Printed: 2026-05-04 21:00  │  ← footer
///   └─────────────────────────────┘
/// </summary>
public class DefaultPrintTemplate : IPrintTemplate
{
    private static readonly Font HeaderFont = new("Arial", 18, FontStyle.Bold);
    private static readonly Font LabelFont = new("Arial", 11, FontStyle.Bold);
    private static readonly Font ValueFont = new("Arial", 11, FontStyle.Regular);
    private static readonly Font FooterFont = new("Arial", 8, FontStyle.Italic);

    private const int Margin = 20;
    private const int LineSpacing = 6;

    public void Render(
        Graphics graphics,
        Rectangle bounds,
        string clinicName,
        string patientName,
        string patientId,
        string studyDescription,
        string modality,
        DateTime? studyDate,
        string? institutionName,
        string? referringPhysician,
        string? imagePath)
    {
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        int y = bounds.Top + Margin;
        int left = bounds.Left + Margin;
        int contentWidth = bounds.Width - (Margin * 2);

        // ── Header: Clinic Name ──
        var headerText = string.IsNullOrWhiteSpace(clinicName) ? "ClariMed" : clinicName;
        var headerSize = graphics.MeasureString(headerText, HeaderFont);
        float headerX = left + (contentWidth - headerSize.Width) / 2;
        graphics.DrawString(headerText, HeaderFont, Brushes.Black, headerX, y);
        y += (int)headerSize.Height + LineSpacing;

        // Separator line
        graphics.DrawLine(Pens.DarkGray, left, y, left + contentWidth, y);
        y += LineSpacing * 2;

        // ── Patient Info Block ──
        y = DrawInfoLine(graphics, left, y, "Patient:", patientName);
        y = DrawInfoLine(graphics, left, y, "Patient ID:", patientId);
        y = DrawInfoLine(graphics, left, y, "Study:", studyDescription);
        y = DrawInfoLine(graphics, left, y, "Modality:", modality);
        y = DrawInfoLine(graphics, left, y, "Study Date:", studyDate?.ToString("yyyy-MM-dd") ?? "N/A");
        
        if (!string.IsNullOrWhiteSpace(institutionName))
            y = DrawInfoLine(graphics, left, y, "Institution:", institutionName);
            
        if (!string.IsNullOrWhiteSpace(referringPhysician))
            y = DrawInfoLine(graphics, left, y, "Physician:", referringPhysician);

        y += LineSpacing;

        // Separator line
        graphics.DrawLine(Pens.DarkGray, left, y, left + contentWidth, y);
        y += LineSpacing * 2;

        // ── Image Area ──
        int footerHeight = 40;
        int availableHeight = bounds.Bottom - Margin - footerHeight - y;

        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            try
            {
                using var image = Image.FromFile(imagePath);

                // Calculate proportional fit within available space
                double scaleX = (double)contentWidth / image.Width;
                double scaleY = (double)availableHeight / image.Height;
                double scale = Math.Min(scaleX, scaleY);
                scale = Math.Min(scale, 1.0); // never upscale

                int drawWidth = (int)(image.Width * scale);
                int drawHeight = (int)(image.Height * scale);
                int drawX = left + (contentWidth - drawWidth) / 2;

                graphics.DrawImage(image, drawX, y, drawWidth, drawHeight);
            }
            catch
            {
                graphics.DrawString("[Image could not be loaded]", ValueFont, Brushes.Gray, left, y);
            }
        }
        else
        {
            graphics.DrawString("[No image available]", ValueFont, Brushes.Gray, left, y);
        }

        // ── Footer ──
        int footerY = bounds.Bottom - Margin - 20;
        graphics.DrawLine(Pens.LightGray, left, footerY - 5, left + contentWidth, footerY - 5);
        var footerText = $"Printed: {DateTime.Now:yyyy-MM-dd HH:mm}  |  ClariMed";
        graphics.DrawString(footerText, FooterFont, Brushes.Gray, left, footerY);
    }

    private int DrawInfoLine(Graphics g, int x, int y, string label, string value)
    {
        var labelSize = g.MeasureString(label, LabelFont);
        g.DrawString(label, LabelFont, Brushes.Black, x, y);
        g.DrawString(value, ValueFont, Brushes.DarkSlateGray, x + (int)labelSize.Width + 5, y);
        return y + (int)labelSize.Height + LineSpacing;
    }
}
