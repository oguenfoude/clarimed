using FocusMed.Printing.Cover;
using FocusMed.Printing.Merging;
using FocusMed.Printing.SilentPrint;
using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Printing;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers FocusMed.Printing services:
    /// - PDF cover page generator (ICoverPageGenerator — QuestPDF)
    /// - PDF merger (IPdfMerger — PdfSharpCore grid layout + A3/booklet imposition)
    /// - Silent PDF printer (ISilentPrinter — PdfiumViewer)
    /// </summary>
    public static IServiceCollection AddFocusMedPrinting(this IServiceCollection services)
    {
        services.AddTransient<ICoverPageGenerator, QuestPdfCoverPageGenerator>();
        services.AddTransient<IPdfMerger, PdfSharpMerger>();
        services.AddTransient<ISilentPrinter, PdfiumSilentPrinter>();

        return services;
    }
}
