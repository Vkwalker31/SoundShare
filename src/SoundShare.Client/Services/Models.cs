// Client-side copies of the REST API contracts (docs/api/endpoints.md).
namespace SoundShare.Client.Services;

public sealed record TokenResponse(string AccessToken, int ExpiresIn);

public sealed record UserDto(long Id, string Username, string Email, string Role, bool IsActive, DateTime CreatedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record RoomSummaryDto(long Id, string Name, int MembersCount);

public sealed record RoomCreatedDto(long Id, string Name, string InviteCode);

public sealed record RoomStateDto(long Id, string Name, bool IsPrivate, string? InviteCode, int MembersCount, string MyRole);

public sealed record TrackDto(long Id, string Title, IReadOnlyList<string> Artists, int DurationSec, string ExternalSourceId, Uri ThumbnailUrl);

public sealed record QueueItemDto(long Id, TrackDto Track, int Score, string AddedBy, bool Pinned, int? Position);

public sealed record PlayingDto(long ItemId, TrackDto Track, DateTime StartedAtUtc, DateTime ServerTimeUtc);

public sealed record QueueDto(long Version, PlayingDto? Playing, IReadOnlyList<QueueItemDto> Items);

public sealed record VoteResultDto(long ItemId, int Score, string Status);

public sealed record SkipProgressDto(int Votes, int Required, bool Skipped);

public sealed record TrackSearchResultDto(IReadOnlyList<TrackDto> Items, bool ExternalSearchAvailable);

public sealed record AuditLogDto(
    long Id,
    DateTime CreatedAt,
    string Source,
    long? UserId,
    string Action,
    string? EntityType,
    string? EntityId,
    string? HttpMethod,
    string? Path,
    int? StatusCode,
    string? IpAddress,
    string? CorrelationId,
    int? DurationMs,
    string? Details);

public sealed record AuditPageDto(IReadOnlyList<AuditLogDto> Items, long? NextCursor);

public sealed record ProblemDto(string? Title, string? Detail, int? Status, Dictionary<string, string[]>? Errors);
