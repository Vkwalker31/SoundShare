using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Domain.Queue;
using SoundShare.Domain.Rooms;

namespace SoundShare.Application.Services;

public sealed class QueueService(
    IQueueRepository queues,
    ICatalogRepository catalog,
    RoomService rooms,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IRoomNotifier notifier,
    IValidator<VoteRequest> voteValidator)
{
    public async Task<QueueDto> GetQueueAsync(long roomId, long userId, CancellationToken cancellationToken)
    {
        await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
        var playlist = await RequireQueueAsync(roomId, cancellationToken);
        return await BuildQueueAsync(playlist, cancellationToken);
    }

    public async Task<QueueItemDto> AddItemAsync(long roomId, long userId, AddQueueItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
        var playlist = await RequireQueueAsync(roomId, cancellationToken);
        var track = await catalog.GetTrackAsync(request.TrackId, cancellationToken) ?? throw new NotFoundException("Track not found.");

        var item = await unitOfWork.InTransactionAsync(
            async ct =>
            {
                var added = await AddWithinLimitAsync(playlist, track.Id, userId, ct);
                await StartNextIfIdleAsync(playlist, ct);
                return added;
            },
            cancellationToken);

        await notifier.QueueChangedAsync(roomId, cancellationToken);
        var view = (await queues.GetActiveItemsAsync(playlist.Id, cancellationToken)).First(v => v.Item.Id == item.Id);
        return ToDto(view);
    }

    public async Task<AddedCountDto> AddFromPlaylistAsync(long roomId, long userId, AddFromPlaylistRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
        var queue = await RequireQueueAsync(roomId, cancellationToken);
        var source = await queues.GetPlaylistAsync(request.PlaylistId, cancellationToken);
        if (source is null || source.OwnerId != userId)
        {
            throw new NotFoundException("Playlist not found.");
        }

        var tracks = (await queues.GetPlaylistItemsAsync(source.Id, cancellationToken))
            .OrderBy(i => i.Position)
            .Select(i => i.TrackId)
            .ToList();

        var added = await unitOfWork.InTransactionAsync(
            async ct =>
            {
                var count = 0;
                var free = QueueRules.MaxActiveItemsPerUser - await queues.CountActiveItemsByUserAsync(queue.Id, userId, ct);
                foreach (var trackId in tracks.Take(Math.Max(free, 0)))
                {
                    await AddWithinLimitAsync(queue, trackId, userId, ct);
                    count++;
                }

                await StartNextIfIdleAsync(queue, ct);
                return count;
            },
            cancellationToken);

        if (added > 0)
        {
            await notifier.QueueChangedAsync(roomId, cancellationToken);
        }

        return new AddedCountDto(added);
    }

    public async Task RemoveItemAsync(long roomId, long userId, long itemId, CancellationToken cancellationToken)
    {
        var member = await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
        var (playlist, item) = await RequireItemAsync(roomId, itemId, cancellationToken);
        if (item.AddedById != userId && member.Role < RoomRole.Moderator)
        {
            throw new ForbiddenException("Only the author or a moderator can remove this track.");
        }

        item.Remove();
        playlist.IncrementVersion();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.QueueChangedAsync(roomId, cancellationToken);
    }

    public async Task<VoteResultDto> VoteAsync(long roomId, long userId, long itemId, VoteRequest request, CancellationToken cancellationToken)
    {
        await voteValidator.ValidateAndThrowAsync(request, cancellationToken);
        await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
        var (playlist, item) = await RequireItemAsync(roomId, itemId, cancellationToken);

        var result = await unitOfWork.InTransactionAsync(
            async ct =>
            {
                await queues.LockItemAsync(item.Id, ct);
                var now = time.UtcNow();
                var vote = await queues.GetVoteAsync(item.Id, userId, ct);
                if (request.Value == 0 && vote is not null)
                {
                    queues.RemoveVote(vote);
                }
                else if (request.Value != 0 && vote is null)
                {
                    queues.AddVote(new TrackVote(item.Id, userId, request.Value, now));
                }
                else if (request.Value != 0 && vote is not null)
                {
                    vote.Change(request.Value, now);
                }

                await unitOfWork.SaveChangesAsync(ct);
                var score = await queues.GetScoreAsync(item.Id, ct);
                item.TryAutoRemove(score);
                await unitOfWork.SaveChangesAsync(ct);
                await queues.IncrementVersionAsync(playlist.Id, ct);
                return new VoteResultDto(item.Id, score, item.Status.ToString());
            },
            cancellationToken);

        await notifier.QueueChangedAsync(roomId, cancellationToken);
        return result;
    }

    public async Task SetPinnedAsync(long roomId, long userId, long itemId, bool pinned, CancellationToken cancellationToken)
    {
        var member = await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
        if (member.Role < RoomRole.Moderator)
        {
            throw new ForbiddenException("Only moderators and the owner can pin tracks.");
        }

        var (playlist, item) = await RequireItemAsync(roomId, itemId, cancellationToken);
        if (pinned)
        {
            item.Pin(time.UtcNow());
        }
        else
        {
            item.Unpin();
        }

        playlist.IncrementVersion();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.QueueChangedAsync(roomId, cancellationToken);
    }

    public async Task<SkipProgressDto> VoteSkipAsync(long roomId, long userId, CancellationToken cancellationToken)
    {
        await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
        var playlist = await RequireQueueAsync(roomId, cancellationToken);

        var progress = await unitOfWork.InTransactionAsync(
            async ct =>
            {
                var playing = await queues.GetPlayingAsync(playlist.Id, ct) ?? throw new ConflictException("Nothing is playing.");
                await queues.LockItemAsync(playing.Id, ct);
                if (!await queues.HasSkipVoteAsync(playing.Id, userId, ct))
                {
                    queues.AddSkipVote(new SkipVote(playing.Id, userId, time.UtcNow()));
                    await unitOfWork.SaveChangesAsync(ct);
                }

                var votes = await queues.CountSkipVotesAsync(playing.Id, ct);
                var online = (await rooms.GetActiveMemberCountAsync(roomId, ct));
                var required = QueueRules.RequiredSkipVotes(online);
                if (!QueueRules.IsSkipReached(votes, online))
                {
                    return new SkipProgressDto(votes, required, false);
                }

                playing.Skip();
                await StartNextAsync(playlist, ct);
                return new SkipProgressDto(votes, required, true);
            },
            cancellationToken);

        if (!progress.Skipped)
        {
            await notifier.SkipProgressAsync(roomId, progress.Votes, progress.Required, cancellationToken);
        }

        await NotifyPlaybackAsync(roomId, progress.Skipped, cancellationToken);
        return progress;
    }

    public async Task SkipAsync(long roomId, long userId, CancellationToken cancellationToken)
    {
        var member = await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
        if (member.Role < RoomRole.Moderator)
        {
            throw new ForbiddenException("Only moderators and the owner can skip tracks.");
        }

        var playlist = await RequireQueueAsync(roomId, cancellationToken);
        await unitOfWork.InTransactionAsync(
            async ct =>
            {
                var playing = await queues.GetPlayingAsync(playlist.Id, ct) ?? throw new ConflictException("Nothing is playing.");
                playing.Skip();
                await StartNextAsync(playlist, ct);
                return true;
            },
            cancellationToken);

        await NotifyPlaybackAsync(roomId, true, cancellationToken);
    }

    public async Task<int> AdvanceFinishedTracksAsync(CancellationToken cancellationToken)
    {
        var now = time.UtcNow();
        var roomIds = await queues.GetRoomIdsWithFinishedTrackAsync(now, cancellationToken);
        foreach (var roomId in roomIds)
        {
            var playlist = await RequireQueueAsync(roomId, cancellationToken);
            var advanced = await unitOfWork.InTransactionAsync(
                async ct =>
                {
                    var playing = await queues.GetPlayingAsync(playlist.Id, ct);
                    if (playing is null || playing.StartedAt?.AddSeconds(playing.Track.DurationSec) > now)
                    {
                        return false;
                    }

                    await queues.LockItemAsync(playing.Id, ct);
                    playing.Finish();
                    await StartNextAsync(playlist, ct);
                    return true;
                },
                cancellationToken);

            await NotifyPlaybackAsync(roomId, advanced, cancellationToken);
        }

        return roomIds.Count;
    }

    private async Task<QueueItem> AddWithinLimitAsync(Playlist playlist, long trackId, long userId, CancellationToken cancellationToken)
    {
        if (await queues.CountActiveItemsByUserAsync(playlist.Id, userId, cancellationToken) >= QueueRules.MaxActiveItemsPerUser)
        {
            throw new ConflictException($"You already have {QueueRules.MaxActiveItemsPerUser} tracks in the queue.");
        }

        var now = time.UtcNow();
        var item = new QueueItem(playlist.Id, trackId, userId, now);
        queues.AddItem(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        queues.AddVote(new TrackVote(item.Id, userId, 1, now));
        playlist.IncrementVersion();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item;
    }

    private async Task StartNextIfIdleAsync(Playlist playlist, CancellationToken cancellationToken)
    {
        if (await queues.GetPlayingAsync(playlist.Id, cancellationToken) is null)
        {
            await StartNextAsync(playlist, cancellationToken);
        }
    }

    private async Task StartNextAsync(Playlist playlist, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var queued = (await queues.GetActiveItemsAsync(playlist.Id, cancellationToken))
            .Where(v => v.Item.Status == QueueItemStatus.Queued)
            .ToList();
        var ordered = QueueRules.Order(queued, v => v.Item.PinnedAt, v => v.Score, v => v.Item.AddedAt);
        if (ordered.Count > 0)
        {
            ordered[0].Item.StartPlaying(time.UtcNow());
        }

        playlist.IncrementVersion();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<QueueDto> BuildQueueAsync(Playlist playlist, CancellationToken cancellationToken)
    {
        var views = await queues.GetActiveItemsAsync(playlist.Id, cancellationToken);
        var playingView = views.FirstOrDefault(v => v.Item.Status == QueueItemStatus.Playing);
        var queued = QueueRules.Order(
            views.Where(v => v.Item.Status == QueueItemStatus.Queued),
            v => v.Item.PinnedAt,
            v => v.Score,
            v => v.Item.AddedAt);

        var playing = playingView is null
            ? null
            : new PlayingDto(playingView.Item.Id, playingView.Item.Track.ToDto(), playingView.Item.StartedAt!.Value, time.UtcNow());
        return new QueueDto(playlist.QueueVersion, playing, queued.Select(ToDto).ToList());
    }

    private static QueueItemDto ToDto(QueueItemView view) =>
        new(view.Item.Id, view.Item.Track.ToDto(), view.Score, view.AddedBy, view.Item.IsPinned, view.Item.Position);

    private async Task<Playlist> RequireQueueAsync(long roomId, CancellationToken cancellationToken) =>
        await queues.GetRoomQueueAsync(roomId, cancellationToken) ?? throw new NotFoundException("Room not found.");

    private async Task<(Playlist Playlist, QueueItem Item)> RequireItemAsync(long roomId, long itemId, CancellationToken cancellationToken)
    {
        var playlist = await RequireQueueAsync(roomId, cancellationToken);
        var item = await queues.GetItemAsync(itemId, cancellationToken);
        if (item is null || item.PlaylistId != playlist.Id)
        {
            throw new NotFoundException("Queue item not found.");
        }

        return (playlist, item);
    }

    private async Task NotifyPlaybackAsync(long roomId, bool playbackChanged, CancellationToken cancellationToken)
    {
        if (playbackChanged)
        {
            await notifier.PlaybackChangedAsync(roomId, cancellationToken);
        }

        await notifier.QueueChangedAsync(roomId, cancellationToken);
    }
}
