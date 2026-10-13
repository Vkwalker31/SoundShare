using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.WebApi.Auditing;

namespace SoundShare.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/users")]
public sealed class AdminUsersController(UserAdminService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> List(string? role, bool? isActive, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        users.ListAsync(role, isActive, page, pageSize, cancellationToken);

    [HttpGet("{userId:long}")]
    public Task<UserDto> Get(long userId, CancellationToken cancellationToken) => users.GetAsync(userId, cancellationToken);

    [HttpPost]
    [AuditAction("Admin.CreateUser")]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { userId = user.Id }, user);
    }

    [HttpPatch("{userId:long}/role")]
    [AuditAction("Admin.ChangeRole")]
    public async Task<IActionResult> ChangeRole(long userId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        await users.ChangeRoleAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:long}/block")]
    [AuditAction("Admin.BlockUser")]
    public async Task<IActionResult> Block(long userId, CancellationToken cancellationToken)
    {
        await users.BlockAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId:long}/block")]
    [AuditAction("Admin.UnblockUser")]
    public async Task<IActionResult> Unblock(long userId, CancellationToken cancellationToken)
    {
        await users.UnblockAsync(userId, cancellationToken);
        return NoContent();
    }
}
