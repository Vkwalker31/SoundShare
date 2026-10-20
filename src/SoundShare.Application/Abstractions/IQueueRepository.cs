using SoundShare.Domain.Queue;

namespace SoundShare.Application.Abstractions;

public sealed record QueueItemView(QueueItem Item, int Score, string AddedBy);

public interface IQueueRepository
{
    Task<Playlist?> GetRoomQueueAsync(long roomId, CancellationToken cancellationToken);

    Task<Playlist?> GetPlaylistAsync(long playlistId, CancellationToken cancellationToken);

    void AddPlaylist(Playlist playlist);

    Task<QueueItem?> GetItemAsync(long itemId, CancellationToken cancellationToken);

    Task LockItemAsync(long itemId, CancellationToken cancellationToken);

    Task IncrementVersionAsync(long playlistId, CancellationToken cancellationToken);

    Task<QueueItem?> GetPlayingAsync(long playlistId, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueItemView>> GetActiveItemsAsync(long playlistId, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueItem>> GetPlaylistItemsAsync(long playlistId, CancellationToken cancellationToken);

    Task<IReadOnlyList<long>> GetRoomIdsWithFinishedTrackAsync(DateTime now, CancellationToken cancellationToken);

    Task<int> CountActiveItemsByUserAsync(long playlistId, long userId, CancellationToken cancellationToken);

    void AddItem(QueueItem item);

    Task<TrackVote?> GetVoteAsync(long itemId, long userId, CancellationToken cancellationToken);

    void AddVote(TrackVote vote);

    void RemoveVote(TrackVote vote);

    Task<int> GetScoreAsync(long itemId, CancellationToken cancellationToken);

    Task<bool> HasSkipVoteAsync(long itemId, long userId, CancellationToken cancellationToken);

    void AddSkipVote(SkipVote vote);

    Task<int> CountSkipVotesAsync(long itemId, CancellationToken cancellationToken);
}
