namespace ClariMed.Dicom.Services;

/// <summary>
/// Controls the DICOM network server lifecycle (start/stop).
/// </summary>
public interface IDicomServer
{
    /// <summary>
    /// Starts the DICOM C-STORE SCP listener on the specified AE Title and port.
    /// </summary>
    Task StartAsync(string aeTitle, int port, CancellationToken cancellationToken);

    /// <summary>
    /// Stops the DICOM server gracefully.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Whether the server is currently listening.
    /// </summary>
    bool IsListening { get; }
}
