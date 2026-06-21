using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfiumViewer;

using FocusMed.Printing.Merging;

namespace FocusMed.Printing.SilentPrint;

public class PdfiumSilentPrinter : ISilentPrinter
{
    private readonly ILogger<PdfiumSilentPrinter> _logger;

    public PdfiumSilentPrinter(ILogger<PdfiumSilentPrinter> logger)
    {
        _logger = logger;
    }

    public Task PrintPdfAsync(string pdfFilePath, string printerName, PrintFormat format = PrintFormat.A4, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(pdfFilePath))
                throw new FileNotFoundException($"PDF file not found: {pdfFilePath}");

            _logger.LogInformation("Silently printing {File} to {Printer}...", Path.GetFileName(pdfFilePath), printerName);

            using var document = PdfDocument.Load(pdfFilePath);
            using var printDocument = document.CreatePrintDocument();

            printDocument.PrinterSettings.PrinterName = printerName;

            if (!printDocument.PrinterSettings.IsValid)
            {
                throw new InvalidOperationException($"Printer '{printerName}' is not valid or not installed.");
            }

            printDocument.Print();

            _logger.LogInformation("Successfully spooled document to {Printer}.", printerName);
        }, cancellationToken);
    }
}
