using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.WebApi.Auditing;
using SoundShare.WebApi.Security;

namespace SoundShare.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/rooms/{roomId:long}")]
public sealed class QueueController(QueueService queue) : ControllerBase
{
    [HttpGet("queue")]
    public async Task<QueueDto> Get(long roomId, CancellationToken cancellationToken)
    {
        var dto = await queue.GetQueueAsync(roomId, User.GetUserId(), cancellationToken);
        Response.Headers.ETag = $"\"{dto.Version.ToString(CultureInfo.InvariantCulture)}\"";
        return dto;
    }

    [HttpPost("queue/items")]
    [AuditAction("Queue.AddTrack")]
    public async Task<ActionResult<QueueItemDto>> Add(long roomId, AddQueueItemRequest request, CancellationToken cancellationToken)
    {
        var item = await queue.AddItemAsync(roomId, User.GetUserId(), request, cancellationToken);
        return Created($"/api/rooms/{roomId}/queue/items/{item.Id}", item);
    }

    [HttpPost("queue/items/from-playlist")]
    [AuditAction("Queue.AddFromPlaylist")]
    public Task<AddedCountDto> AddFromPlaylist(long roomId, AddFromPlaylistRequest request, CancellationToken cancellationToken) =>
        queue.AddFromPlaylistAsync(roomId, User.GetUserId(), request, cancellationToken);

    [HttpDelete("queue/items/{itemId:long}")]
    [AuditAction("Queue.RemoveTrack")]
    public async Task<IActionResult> Remove(long roomId, long itemId, CancellationToken cancellationToken)
    {
        await queue.RemoveItemAsync(roomId, User.GetUserId(), itemId, cancellationToken);
        return NoContent();
    }

    [HttpPut("queue/items/{itemId:long}/vote")]
    [AuditAction("Queue.Vote")]
    public Task<VoteResultDto> Vote(long roomId, long itemId, VoteRequest request, CancellationToken cancellationToken) =>
        queue.VoteAsync(roomId, User.GetUserId(), itemId, request, cancellationToken);

    [HttpPut("queue/items/{itemId:long}/pin")]
    [AuditAction("Queue.Pin")]
    public async Task<IActionResult> Pin(long roomId, long itemId, CancellationToken cancellationToken)
    {
        await queue.SetPinnedAsync(roomId, User.GetUserId(), itemId, true, cancellationToken);
        return NoContent();
    }

    [HttpDelete("queue/items/{itemId:long}/pin")]
    [AuditAction("Queue.Unpin")]
    public async Task<IActionResult> Unpin(long roomId, long itemId, CancellationToken cancellationToken)
    {
        await queue.SetPinnedAsync(roomId, User.GetUserId(), itemId, false, cancellationToken);
        return NoContent();
    }

    [HttpPost("playback/skip-votes")]
    [AuditAction("Playback.SkipVote")]
    public Task<SkipProgressDto> VoteSkip(long roomId, CancellationToken cancellationToken) =>
        queue.VoteSkipAsync(roomId, User.GetUserId(), cancellationToken);

    [HttpPost("playback/skip")]
    [AuditAction("Playback.Skip")]
    public async Task<IActionResult> Skip(long roomId, CancellationToken cancellationToken)
    {
        await queue.SkipAsync(roomId, User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
