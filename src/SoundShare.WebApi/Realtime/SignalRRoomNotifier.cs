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
}
