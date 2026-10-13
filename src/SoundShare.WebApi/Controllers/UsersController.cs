using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.WebApi.Auditing;
using SoundShare.WebApi.Security;

namespace SoundShare.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(UserAdminService users) : ControllerBase
{
    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken cancellationToken) => users.GetAsync(User.GetUserId(), cancellationToken);

    [HttpDelete("me")]
    [AuditAction("User.DeleteAccount")]
    public async Task<IActionResult> DeleteMe(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        await users.DeleteOwnAccountAsync(User.GetUserId(), request, cancellationToken);
        return NoContent();
    }
}
