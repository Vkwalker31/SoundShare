using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Domain.Queue;

namespace SoundShare.Application.Services;

public sealed class PlaylistService(
    IQueueRepository queues,
    RoomService rooms,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IValidator<CreatePlaylistRequest> validator)
{
    public async Task<PlaylistDto> CreateAsync(long userId, CreatePlaylistRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        IReadOnlyList<long> trackIds = [];
        if (request.FromRoomId is { } roomId)
        {
            await rooms.RequireActiveMemberAsync(roomId, userId, cancellationToken);
            var queue = await queues.GetRoomQueueAsync(roomId, cancellationToken) ?? throw new NotFoundException("Room not found.");
            var active = await queues.GetActiveItemsAsync(queue.Id, cancellationToken);
            trackIds = QueueRules.Order(active, v => v.Item.PinnedAt, v => v.Score, v => v.Item.AddedAt)
                .OrderBy(v => v.Item.Status != QueueItemStatus.Playing)
                .Select(v => v.Item.TrackId)
                .ToList();
        }

        var playlistId = await unitOfWork.InTransactionAsync(
            async ct =>
            {
                var now = time.UtcNow();
                var playlist = Playlist.ForUser(userId, request.Name.Trim(), now);
                queues.AddPlaylist(playlist);
                await unitOfWork.SaveChangesAsync(ct);
                var position = 1;
                foreach (var trackId in trackIds)
                {
                    queues.AddItem(new QueueItem(playlist.Id, trackId, userId, now, position++));
                }

                await unitOfWork.SaveChangesAsync(ct);
                return playlist.Id;
            },
            cancellationToken);

        return await GetAsync(userId, playlistId, cancellationToken);
    }

    public async Task<PlaylistDto> GetAsync(long userId, long playlistId, CancellationToken cancellationToken)
    {
        var playlist = await queues.GetPlaylistAsync(playlistId, cancellationToken);
        if (playlist is null || playlist.OwnerId != userId)
        {
            throw new NotFoundException("Playlist not found.");
        }

        var items = await queues.GetPlaylistItemsAsync(playlistId, cancellationToken);
        return new PlaylistDto(
            playlist.Id,
            playlist.Name,
            items.OrderBy(i => i.Position)
                .Select(i => new QueueItemDto(i.Id, i.Track.ToDto(), 0, string.Empty, false, i.Position))
                .ToList());
    }
}
