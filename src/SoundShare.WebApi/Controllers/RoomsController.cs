using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.WebApi.Auditing;
using SoundShare.WebApi.Security;

namespace SoundShare.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/rooms")]
public sealed class RoomsController(RoomService rooms) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RoomSummaryDto>> List(CancellationToken cancellationToken) => rooms.ListPublicAsync(cancellationToken);

    [HttpPost]
    [AuditAction("Room.Create")]
    public async Task<ActionResult<RoomCreatedDto>> Create(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var room = await rooms.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { roomId = room.Id }, room);
    }

    [HttpGet("{roomId:long}")]
    public Task<RoomStateDto> Get(long roomId, CancellationToken cancellationToken) =>
        rooms.GetStateAsync(roomId, User.GetUserId(), cancellationToken);

    [HttpPost("{roomId:long}/members")]
    [AuditAction("Room.Join")]
    public Task<RoomStateDto> Join(long roomId, CancellationToken cancellationToken) =>
        rooms.JoinPublicAsync(roomId, User.GetUserId(), cancellationToken);

    [HttpPost("join")]
    [AuditAction("Room.JoinByCode")]
    public Task<RoomStateDto> JoinByCode(JoinByCodeRequest request, CancellationToken cancellationToken) =>
        rooms.JoinByCodeAsync(User.GetUserId(), request, cancellationToken);

    [HttpDelete("{roomId:long}/members/me")]
    [AuditAction("Room.Leave")]
    public async Task<IActionResult> Leave(long roomId, CancellationToken cancellationToken)
    {
        await rooms.LeaveAsync(roomId, User.GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPatch("{roomId:long}/members/{userId:long}")]
    [AuditAction("Room.ChangeMemberRole")]
    public async Task<IActionResult> ChangeMemberRole(long roomId, long userId, ChangeMemberRoleRequest request, CancellationToken cancellationToken)
    {
        await rooms.ChangeMemberRoleAsync(roomId, User.GetUserId(), userId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{roomId:long}/bans")]
    [AuditAction("Room.Ban")]
    public async Task<IActionResult> Ban(long roomId, BanRequest request, CancellationToken cancellationToken)
    {
        await rooms.BanAsync(roomId, User.GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{roomId:long}")]
    [AuditAction("Room.Close")]
    public async Task<IActionResult> Close(long roomId, CancellationToken cancellationToken)
    {
        await rooms.CloseAsync(roomId, User.GetUserId(), User.IsAdmin(), cancellationToken);
        return NoContent();
    }
}
