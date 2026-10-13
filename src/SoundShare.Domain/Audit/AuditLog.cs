namespace SoundShare.Domain.Audit;

public sealed class AuditLog
{
    public long Id { get; init; }

    public DateTime CreatedAt { get; init; }

    public string Source { get; init; } = null!;

    public long? UserId { get; init; }

    public string Action { get; init; } = null!;

    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public string? HttpMethod { get; init; }

    public string? Path { get; init; }

    public int? StatusCode { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? CorrelationId { get; init; }

    public int? DurationMs { get; init; }

    public string? Details { get; init; }
}
