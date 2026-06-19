using FellowOakDicom.Network;
using Microsoft.Extensions.Logging;

namespace FocusMed.Dicom.Services;

/// <summary>
/// Wraps the fo-dicom DicomServer to manage the DICOM C-STORE SCP listener.
/// </summary>
public class DicomServer : IDicomServer, IDisposable
{
    private readonly ILogger<DicomServer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private FellowOakDicom.Network.IDicomServer? _server;

    public bool IsListening => _server != null;

    public DicomServer(ILogger<DicomServer> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(string aeTitle, int port, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DICOM server — AE Title: {AETitle}, Port: {Port}", aeTitle, port);

        _server = FellowOakDicom.Network.DicomServerFactory.Create<Handlers.CStoreScp>(port);

        _logger.LogInformation("DICOM server is listening on port {Port}", port);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_server != null)
        {
            _logger.LogInformation("Stopping DICOM server...");
            (_server as IDisposable)?.Dispose();
            _server = null;
            _logger.LogInformation("DICOM server stopped.");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        (_server as IDisposable)?.Dispose();
        _server = null;
    }
}
