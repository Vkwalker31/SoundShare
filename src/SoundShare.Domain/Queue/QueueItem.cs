using SoundShare.Domain.Catalog;
using SoundShare.Domain.Common;

namespace SoundShare.Domain.Queue;

public sealed class QueueItem
{
    private QueueItem()
    {
    }

    public QueueItem(long playlistId, long trackId, long addedById, DateTime addedAt, int? position = null)
    {
        PlaylistId = playlistId;
        TrackId = trackId;
        AddedById = addedById;
        AddedAt = addedAt;
        Status = QueueItemStatus.Queued;
        Position = position;
    }

    public long Id { get; private set; }

    public long PlaylistId { get; private set; }

    public long TrackId { get; private set; }

    public long AddedById { get; private set; }

    public DateTime AddedAt { get; private set; }

    public QueueItemStatus Status { get; private set; }

    public DateTime? PinnedAt { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public int? Position { get; private set; }

    public Track Track { get; private set; } = null!;

    public bool IsPinned => PinnedAt is not null;

    public bool IsActive => Status is QueueItemStatus.Queued or QueueItemStatus.Playing;

    public void StartPlaying(DateTime now)
    {
        EnsureStatus(QueueItemStatus.Queued, nameof(StartPlaying));
        Status = QueueItemStatus.Playing;
        StartedAt = now;
        PinnedAt = null;
    }

    public void Finish()
    {
        EnsureStatus(QueueItemStatus.Playing, nameof(Finish));
        Status = QueueItemStatus.Played;
    }

    public void Skip()
    {
        EnsureStatus(QueueItemStatus.Playing, nameof(Skip));
        Status = QueueItemStatus.Skipped;
    }

    public void Remove()
    {
        EnsureStatus(QueueItemStatus.Queued, nameof(Remove));
        Status = QueueItemStatus.Removed;
    }

    public bool TryAutoRemove(int score)
    {
        if (Status != QueueItemStatus.Queued || !QueueRules.ShouldAutoRemove(score, IsPinned))
        {
            return false;
        }

        Status = QueueItemStatus.AutoRemoved;
        return true;
    }

    public void Pin(DateTime now)
    {
        EnsureStatus(QueueItemStatus.Queued, nameof(Pin));
        PinnedAt ??= now < AddedAt ? AddedAt : now;
    }

    public void Unpin() => PinnedAt = null;

    private void EnsureStatus(QueueItemStatus expected, string operation)
    {
        if (Status != expected)
        {
            throw new DomainException($"Cannot {operation} a queue item in status {Status}.");
        }
    }
}
