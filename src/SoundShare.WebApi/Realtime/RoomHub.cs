using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SoundShare.Application.Services;
using SoundShare.WebApi.Security;

namespace SoundShare.WebApi.Realtime;

[Authorize]
internal sealed class RoomHub(RoomService rooms) : Hub
{
    public static string GroupName(long roomId) => $"room:{roomId}";

    public async Task JoinRoom(long roomId)
    {
        await rooms.RequireActiveMemberAsync(roomId, Context.User!.GetUserId(), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId), Context.ConnectionAborted);
    }

    public Task LeaveRoom(long roomId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(roomId), Context.ConnectionAborted);
}
