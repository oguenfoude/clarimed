using ClariMed.Dicom.Services;
using Microsoft.Extensions.DependencyInjection;

using FellowOakDicom;

namespace ClariMed.Dicom;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ClariMed.Dicom services: IDicomServer.
    /// </summary>
    public static IServiceCollection AddClariMedDicom(this IServiceCollection services)
    {
        services.AddFellowOakDicom()
                .AddImageManager<FellowOakDicom.Imaging.WinFormsImageManager>()
                .AddTranscoderManager<FellowOakDicom.Imaging.NativeCodec.NativeTranscoderManager>();
        services.AddSingleton<IDicomServer, Services.DicomServer>();
        services.AddTransient<DicomFileIngestionService>();
        return services;
    }
}
