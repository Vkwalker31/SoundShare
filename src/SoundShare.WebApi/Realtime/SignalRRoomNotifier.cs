using Microsoft.AspNetCore.SignalR;
using SoundShare.Application.Abstractions;

namespace SoundShare.WebApi.Realtime;

internal sealed class SignalRRoomNotifier(IHubContext<RoomHub> hub) : IRoomNotifier
{
    public Task QueueChangedAsync(long roomId, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.GroupName(roomId)).SendAsync("QueueUpdated", new { roomId }, cancellationToken);

    public Task PlaybackChangedAsync(long roomId, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.GroupName(roomId)).SendAsync("PlaybackChanged", new { roomId, serverTimeUtc = DateTime.UtcNow }, cancellationToken);

    public Task RoomClosedAsync(long roomId, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.GroupName(roomId)).SendAsync("RoomClosed", new { roomId }, cancellationToken);

    public Task SkipProgressAsync(long roomId, int votes, int required, CancellationToken cancellationToken) =>
        hub.Clients.Group(RoomHub.GroupName(roomId)).SendAsync("SkipProgress", new { votes, required, skipped = false }, cancellationToken);

    public Task MemberRemovedAsync(long roomId, long userId, string reason, CancellationToken cancellationToken) =>
        hub.Clients.User(userId.ToString(System.Globalization.CultureInfo.InvariantCulture)).SendAsync("RemovedFromRoom", new { roomId, reason }, cancellationToken);
}
