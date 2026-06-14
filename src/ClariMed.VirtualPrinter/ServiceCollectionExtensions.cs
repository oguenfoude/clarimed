using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.VirtualPrinter;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClariMedVirtualPrinter(this IServiceCollection services)
    {
        services.AddSingleton<WindowsPrinterRegistration>();
        return services;
    }
}
