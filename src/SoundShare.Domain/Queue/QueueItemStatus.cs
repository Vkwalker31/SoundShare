namespace SoundShare.Domain.Queue;

public enum QueueItemStatus
{
    Queued,
    Playing,
    Played,
    Skipped,
    Removed,
    AutoRemoved,
}
