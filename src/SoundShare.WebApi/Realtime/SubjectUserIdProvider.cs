// SignalR addresses users by the JWT "sub" claim (inbound claim mapping is disabled).
using Microsoft.AspNetCore.SignalR;

namespace SoundShare.WebApi.Realtime;

internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}
