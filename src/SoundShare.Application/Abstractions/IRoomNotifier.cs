namespace SoundShare.Application.Abstractions;

public interface IRoomNotifier
{
    Task QueueChangedAsync(long roomId, CancellationToken cancellationToken);

    Task PlaybackChangedAsync(long roomId, CancellationToken cancellationToken);

    Task RoomClosedAsync(long roomId, CancellationToken cancellationToken);

    Task SkipProgressAsync(long roomId, int votes, int required, CancellationToken cancellationToken);

    Task MemberRemovedAsync(long roomId, long userId, string reason, CancellationToken cancellationToken);
}
