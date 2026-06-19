using System.Drawing;
using System.Drawing.Printing;
using FocusMed.Printing.Templates;

namespace FocusMed.Printing.Engines;

/// <summary>
/// Sends rendered medical reports to a Windows printer using GDI+ PrintDocument.
/// </summary>
public class WindowsPrintEngine : IPrintEngine
{
    private readonly IPrintTemplate _template;

    public WindowsPrintEngine(IPrintTemplate template)
    {
        _template = template;
    }

    public Task<bool> PrintAsync(
        string printerName,
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
        return Task.Run(() =>
        {
            try
            {
                using var doc = new PrintDocument();

                if (!string.IsNullOrWhiteSpace(printerName))
                {
                    doc.PrinterSettings.PrinterName = printerName;
                }

                if (!doc.PrinterSettings.IsValid)
                {
                    throw new InvalidOperationException(
                        $"Printer '{doc.PrinterSettings.PrinterName}' is not available.");
                }

                doc.DocumentName = $"FocusMed - {patientName} - {studyDescription}";

                doc.PrintPage += (sender, e) =>
                {
                    if (e.Graphics == null) return;

                    var bounds = e.MarginBounds;

                    _template.Render(
                        e.Graphics,
                        bounds,
                        clinicName,
                        patientName,
                        patientId,
                        studyDescription,
                        modality,
                        studyDate,
                        institutionName,
                        referringPhysician,
                        imagePath);

                    e.HasMorePages = false;
                };

                doc.Print();
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public IReadOnlyList<string> GetAvailablePrinters()
    {
        var printers = new List<string>();

        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            printers.Add(printer);
        }

        return printers.AsReadOnly();
    }
}
