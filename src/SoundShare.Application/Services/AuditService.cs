using SoundShare.Application.Abstractions;
using SoundShare.Application.Contracts;

namespace SoundShare.Application.Services;

public sealed class AuditService(IAuditRepository audit)
{
    public async Task<AuditPageDto> ListAsync(long? userId, string? action, DateTime? since, DateTime? until, int pageSize, long? cursor, CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var rows = await audit.ListAsync(userId, action, since, until, cursor, pageSize + 1, cancellationToken);
        var page = rows.Take(pageSize).Select(r => r.ToDto()).ToList();
        var next = rows.Count > pageSize ? page[^1].Id : (long?)null;
        return new AuditPageDto(page, next);
    }
}
