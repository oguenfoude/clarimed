using ClariMed.Documents.Converters;
using ClariMed.Documents.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClariMed.Documents;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ClariMed.Documents services:
    /// - DocumentIngestionQueue (singleton — shared channel between producers and consumer)
    /// - IDocumentConverter (singleton — stateless, thread-safe)
    /// 
    /// Note: IDocumentIngestionChannel producers are registered by the Worker layer,
    /// which controls the folder paths and lifecycle.
    /// </summary>
    public static IServiceCollection AddClariMedDocuments(this IServiceCollection services)
    {
        services.AddSingleton<DocumentIngestionQueue>();
        services.AddSingleton<IDocumentConverter, SpireDocumentConverter>();

        return services;
    }
}
