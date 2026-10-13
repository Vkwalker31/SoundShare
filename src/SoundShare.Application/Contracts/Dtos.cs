namespace SoundShare.Application.Contracts;

public sealed record RegisterRequest(string Username, string Email, string Password);

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResult(string AccessToken, int ExpiresIn, string RefreshToken, DateTime RefreshTokenExpiresAt);

public sealed record UserDto(long Id, string Username, string Email, string Role, bool IsActive, DateTime CreatedAt);

public sealed record CreateUserRequest(string Username, string Email, string Password, string Role);

public sealed record ChangeRoleRequest(string Role);

public sealed record DeleteAccountRequest(string Password);

public sealed record RoomSummaryDto(long Id, string Name, int MembersCount);

public sealed record CreateRoomRequest(string Name, bool IsPrivate);

public sealed record RoomCreatedDto(long Id, string Name, string InviteCode);

public sealed record RoomStateDto(long Id, string Name, bool IsPrivate, string? InviteCode, int MembersCount, string MyRole);

public sealed record JoinByCodeRequest(string InviteCode);

public sealed record BanRequest(long UserId, int? DurationMinutes);

public sealed record ChangeMemberRoleRequest(string Role);

public sealed record TrackDto(long Id, string Title, IReadOnlyList<string> Artists, int DurationSec, string ExternalSourceId, Uri ThumbnailUrl);

public sealed record QueueItemDto(long Id, TrackDto Track, int Score, string AddedBy, bool Pinned, int? Position);

public sealed record PlayingDto(long ItemId, TrackDto Track, DateTime StartedAtUtc, DateTime ServerTimeUtc);

public sealed record QueueDto(long Version, PlayingDto? Playing, IReadOnlyList<QueueItemDto> Items);

public sealed record AddQueueItemRequest(long TrackId);

public sealed record AddFromPlaylistRequest(long PlaylistId);

public sealed record AddedCountDto(int Added);

public sealed record VoteRequest(int Value);

public sealed record VoteResultDto(long ItemId, int Score, string Status);

public sealed record SkipProgressDto(int Votes, int Required, bool Skipped);

public sealed record TrackSearchResultDto(IReadOnlyList<TrackDto> Items, bool ExternalSearchAvailable);

public sealed record CreatePlaylistRequest(string Name, long? FromRoomId);

public sealed record PlaylistDto(long Id, string Name, IReadOnlyList<QueueItemDto> Items);

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
