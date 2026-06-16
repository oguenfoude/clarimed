using ClariMed.Data.Interceptors;
using ClariMed.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClariMedData(this IServiceCollection services, string databasePath)
    {
        // Register the WAL interceptor as a singleton so every connection gets the same PRAGMAs
        services.AddSingleton<WalModeInterceptor>();

        services.AddDbContext<ClariMedDbContext>((sp, options) =>
        {
            options.UseSqlite(
                $"Data Source={databasePath}",
                b => b.MigrationsAssembly("ClariMed.Data"));
            options.AddInterceptors(sp.GetRequiredService<WalModeInterceptor>());
            options.ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        // Repositories (scoped — one per DI scope, matching DbContext lifetime)
        services.AddScoped<IStudyRepository, StudyRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();

        services.AddScoped<IClinicSettingsRepository, ClinicSettingsRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<ClariMed.Data.Repositories.IInboxDocumentRepository, ClariMed.Data.Repositories.InboxDocumentRepository>();

        return services;
    }
}

