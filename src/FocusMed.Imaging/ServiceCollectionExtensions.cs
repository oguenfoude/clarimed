using FocusMed.Imaging.Converters;
using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Imaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers FocusMed.Imaging services: IImageConverter and IThumbnailGenerator.
    /// </summary>
    public static IServiceCollection AddFocusMedImaging(this IServiceCollection services)
    {
        services.AddSingleton<IImageConverter, Converters.ImageConverter>();
        return services;
    }
}
