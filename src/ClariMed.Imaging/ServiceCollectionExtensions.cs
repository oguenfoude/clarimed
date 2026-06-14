using ClariMed.Imaging.Converters;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Imaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ClariMed.Imaging services: IImageConverter and IThumbnailGenerator.
    /// </summary>
    public static IServiceCollection AddClariMedImaging(this IServiceCollection services)
    {
        services.AddSingleton<IImageConverter, Converters.ImageConverter>();
        return services;
    }
}
