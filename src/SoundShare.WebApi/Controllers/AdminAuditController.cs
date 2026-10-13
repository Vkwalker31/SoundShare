using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;

namespace SoundShare.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditController(AuditService audit) : ControllerBase
{
    [HttpGet]
    public Task<AuditPageDto> List(long? userId, string? action, DateTime? from, DateTime? to, int pageSize = 50, long? cursor = null, CancellationToken cancellationToken = default) =>
        audit.ListAsync(userId, action, from?.ToUniversalTime(), to?.ToUniversalTime(), pageSize, cursor, cancellationToken);
}
