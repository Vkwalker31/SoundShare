using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.WebApi.Auditing;
using SoundShare.WebApi.Security;

namespace SoundShare.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class CatalogController(CatalogService catalog, PlaylistService playlists) : ControllerBase
{
    [HttpGet("tracks")]
    public Task<TrackSearchResultDto> Search(string? query, CancellationToken cancellationToken) =>
        catalog.SearchAsync(query, cancellationToken);

    [HttpPost("playlists")]
    [AuditAction("Playlist.Create")]
    public async Task<ActionResult<PlaylistDto>> CreatePlaylist(CreatePlaylistRequest request, CancellationToken cancellationToken)
    {
        var playlist = await playlists.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetPlaylist), new { playlistId = playlist.Id }, playlist);
    }

    [HttpGet("playlists/{playlistId:long}")]
    public Task<PlaylistDto> GetPlaylist(long playlistId, CancellationToken cancellationToken) =>
        playlists.GetAsync(User.GetUserId(), playlistId, cancellationToken);
}
