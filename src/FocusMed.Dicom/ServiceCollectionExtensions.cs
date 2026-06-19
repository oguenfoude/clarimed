using FocusMed.Dicom.Services;
using Microsoft.Extensions.DependencyInjection;

using FellowOakDicom;

namespace FocusMed.Dicom;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers FocusMed.Dicom services: IDicomServer.
    /// </summary>
    public static IServiceCollection AddFocusMedDicom(this IServiceCollection services)
    {
        services.AddFellowOakDicom()
                .AddImageManager<FellowOakDicom.Imaging.WinFormsImageManager>()
                .AddTranscoderManager<FellowOakDicom.Imaging.NativeCodec.NativeTranscoderManager>();
        services.AddSingleton<IDicomServer, Services.DicomServer>();
        services.AddTransient<DicomFileIngestionService>();
        return services;
    }
}
