using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Imaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// FocusMed.Imaging — DICOM-to-PNG conversion is handled natively by fo-dicom
    /// in CStoreScp and DicomFileIngestionService. No additional services needed.
    /// </summary>
    public static IServiceCollection AddFocusMedImaging(this IServiceCollection services)
    {
        return services;
    }
}
