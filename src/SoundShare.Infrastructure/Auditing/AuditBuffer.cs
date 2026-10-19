using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SoundShare.Domain.Audit;

namespace SoundShare.Infrastructure.Auditing;

public sealed partial class AuditBuffer(ILogger<AuditBuffer> logger)
{
    public const int Capacity = 10_000;

    private readonly Channel<AuditLog> _channel = Channel.CreateBounded<AuditLog>(
        new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private long _dropped;

    public long Dropped => Interlocked.Read(ref _dropped);

    internal ChannelReader<AuditLog> Reader => _channel.Reader;

    public void Enqueue(AuditLog entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_channel.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _dropped);
            LogDropped(logger, entry.Action);
        }
    }

    internal void Complete() => _channel.Writer.TryComplete();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit queue is full, entry {Action} dropped")]
    private static partial void LogDropped(ILogger logger, string action);
}
