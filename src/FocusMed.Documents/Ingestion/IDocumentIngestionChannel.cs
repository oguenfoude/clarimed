namespace FocusMed.Documents.Ingestion;

/// <summary>
/// Abstraction for a document ingestion source. Each implementation
/// represents one way documents can enter the system.
///
/// Implementations:
///   - WatchFolderIngestionChannel: monitors a local folder (current)
///   - ApiIngestionChannel: accepts uploads via HTTP API (future Dashboard)
/// </summary>
public interface IDocumentIngestionChannel
{
    /// <summary>
    /// Start producing documents into the shared <see cref="DocumentIngestionQueue"/>.
    /// Called once when the hosting service starts.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stop producing documents and release resources.
    /// Called once when the hosting service stops.
    /// </summary>
    Task StopAsync();
}
