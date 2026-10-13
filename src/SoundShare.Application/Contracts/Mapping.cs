using SoundShare.Domain.Audit;
using SoundShare.Domain.Catalog;
using SoundShare.Domain.Users;

namespace SoundShare.Application.Contracts;

internal static class Mapping
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Username, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);

    public static TrackDto ToDto(this Track track) =>
        new(
            track.Id,
            track.Title,
            track.Artists.OrderBy(a => a.ArtistOrder).Select(a => a.Artist.Name).ToList(),
            track.DurationSec,
            track.ExternalSourceId,
            track.ThumbnailUrl);

    public static AuditLogDto ToDto(this AuditLog log) =>
        new(log.Id, log.CreatedAt, log.Source, log.UserId, log.Action, log.EntityType, log.EntityId,
            log.HttpMethod, log.Path, log.StatusCode, log.IpAddress, log.CorrelationId, log.DurationMs, log.Details);
}
