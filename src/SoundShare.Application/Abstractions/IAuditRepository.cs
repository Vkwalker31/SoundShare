using SoundShare.Domain.Audit;

namespace SoundShare.Application.Abstractions;

public interface IAuditRepository
{
    Task<IReadOnlyList<AuditLog>> ListAsync(long? userId, string? action, DateTime? since, DateTime? until, long? beforeId, int take, CancellationToken cancellationToken);

    Task AddRangeAsync(IReadOnlyCollection<AuditLog> entries, CancellationToken cancellationToken);
}
