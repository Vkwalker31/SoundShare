using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.WebApi.Auditing;
using SoundShare.WebApi.Security;

namespace SoundShare.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    private const string RefreshCookie = "rt";

    [HttpPost("register")]
    [AuditAction("Auth.Register")]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await auth.RegisterAsync(request, cancellationToken);
        return Created($"/api/users/{user.Id}", new { user.Id, user.Username });
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [AuditAction("Auth.Login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Issue(result);
    }

    [HttpPost("refresh")]
    [AuditAction("Auth.Refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookie], HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Issue(result);
    }

    [HttpPost("logout")]
    [AuditAction("Auth.Logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], cancellationToken);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(DateTimeOffset.UnixEpoch));
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    [AuditAction("Auth.LogoutAll")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await auth.LogoutAllAsync(User.GetUserId(), cancellationToken);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(DateTimeOffset.UnixEpoch));
        return NoContent();
    }

    private OkObjectResult Issue(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookie, result.RefreshToken, CookieOptions(result.RefreshTokenExpiresAt));
        return Ok(new { result.AccessToken, result.ExpiresIn });
    }

    private static CookieOptions CookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires,
    };
}
