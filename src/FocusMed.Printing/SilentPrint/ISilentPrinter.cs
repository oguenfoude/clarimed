using System.Threading;
using System.Threading.Tasks;

using FocusMed.Printing.Merging;

namespace FocusMed.Printing.SilentPrint;

public interface ISilentPrinter
{
    Task PrintPdfAsync(string pdfFilePath, string printerName, PrintFormat format = PrintFormat.A4, CancellationToken cancellationToken = default);
}
