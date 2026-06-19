using FocusMed.Data.Interceptors;
using FocusMed.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFocusMedData(this IServiceCollection services, string databasePath)
    {
        // Register the WAL interceptor as a singleton so every connection gets the same PRAGMAs
        services.AddSingleton<WalModeInterceptor>();

        services.AddDbContext<FocusMedDbContext>((sp, options) =>
        {
            options.UseSqlite(
                $"Data Source={databasePath}",
                b => b.MigrationsAssembly("FocusMed.Data"));
            options.AddInterceptors(sp.GetRequiredService<WalModeInterceptor>());
            options.ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        // Repositories (scoped — one per DI scope, matching DbContext lifetime)
        services.AddScoped<IStudyRepository, StudyRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();

        services.AddScoped<IClinicSettingsRepository, ClinicSettingsRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}

