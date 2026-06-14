using ClariMed.Printing.Cover;
using ClariMed.Printing.Engines;
using ClariMed.Printing.Merging;

using ClariMed.Printing.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Printing;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all ClariMed.Printing services:
    /// - Legacy GDI print engine (IPrintTemplate + IPrintEngine)
    /// - PDF cover page generator (ICoverPageGenerator — QuestPDF)
    /// - PDF merger (IPdfMerger — PdfSharpCore)
    /// - Silent PDF printer (ISilentPdfPrinter — PdfiumViewer)
    /// </summary>
    public static IServiceCollection AddClariMedPrinting(this IServiceCollection services)
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

