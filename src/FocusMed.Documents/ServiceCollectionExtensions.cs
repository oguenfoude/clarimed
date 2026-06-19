using FocusMed.Documents.Converters;
using FocusMed.Documents.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FocusMed.Documents;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers FocusMed.Documents services:
    /// - DocumentIngestionQueue (singleton — shared channel between producers and consumer)
    /// - IDocumentConverter (singleton — stateless, thread-safe)
    /// 
    /// Note: IDocumentIngestionChannel producers are registered by the Worker layer,
    /// which controls the folder paths and lifecycle.
    /// </summary>
    public static IServiceCollection AddFocusMedDocuments(this IServiceCollection services)
    {
        services.AddSingleton<DocumentIngestionQueue>();
        services.AddSingleton<IDocumentConverter, SpireDocumentConverter>();

        return services;
    }
}
