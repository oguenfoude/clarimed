using System.Threading.Channels;

namespace ClariMed.Documents.Ingestion;

/// <summary>
/// Shared, bounded, thread-safe queue for incoming documents.
/// Registered as a singleton — all producers write to it, one consumer drains it.
///
/// Capacity: 100 items. If the queue is full, producers will wait (backpressure).
/// Multiple producers are supported (WatchFolder + future API), single consumer.
/// </summary>
public sealed class DocumentIngestionQueue
{
    private readonly Channel<IncomingDocument> _channel;

    public DocumentIngestionQueue()
    {
        _channel = Channel.CreateBounded<IncomingDocument>(
            new BoundedChannelOptions(capacity: 100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,   // Multiple producers allowed
                SingleReader = true     // One processing service drains the queue
            });
    }

    /// <summary>Producers write documents here.</summary>
    public ChannelWriter<IncomingDocument> Writer => _channel.Writer;

    /// <summary>The consumer reads documents here.</summary>
    public ChannelReader<IncomingDocument> Reader => _channel.Reader;
}
