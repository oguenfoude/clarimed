using FocusMed.Printing.Cover;
using FocusMed.Printing.Engines;
using FocusMed.Printing.Merging;

using FocusMed.Printing.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Printing;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all FocusMed.Printing services:
    /// - Legacy GDI print engine (IPrintTemplate + IPrintEngine)
    /// - PDF cover page generator (ICoverPageGenerator — QuestPDF)
    /// - PDF merger (IPdfMerger — PdfSharpCore)
    /// - Silent PDF printer (ISilentPdfPrinter — PdfiumViewer)
    /// </summary>
    public static IServiceCollection AddFocusMedPrinting(this IServiceCollection services)
    {
        // Legacy GDI printing path (kept for backward compatibility)
        services.AddSingleton<IPrintTemplate, DefaultPrintTemplate>();
        services.AddSingleton<IPrintEngine, WindowsPrintEngine>();

        // New PDF pipeline
        services.AddSingleton<ICoverPageGenerator, QuestPdfCoverPageGenerator>();
        services.AddSingleton<IPdfMerger, PdfSharpMerger>();


        return services;
    }
}

