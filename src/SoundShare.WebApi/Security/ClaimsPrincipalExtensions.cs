using System.Globalization;
using System.Security.Claims;
using SoundShare.Application.Common;

namespace SoundShare.WebApi.Security;

internal static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirst("sub")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new AuthenticationFailedException("User id claim is missing.");

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole("Admin");
}
