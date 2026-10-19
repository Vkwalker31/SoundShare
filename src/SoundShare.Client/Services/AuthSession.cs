// In-memory session: the access token lives only in memory, the refresh token in an HttpOnly cookie.
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SoundShare.Client.Services;

public sealed class AuthSession
{
    public string? AccessToken { get; private set; }

    public string? Username { get; private set; }

    public string? Role { get; private set; }

    public long UserId { get; private set; }

    public bool IsAuthenticated => AccessToken is not null;

    public bool IsAdmin => Role == "Admin";

    public event EventHandler? Changed;

    public void SignIn(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        var claims = ReadClaims(accessToken);
        AccessToken = accessToken;
        Username = claims.TryGetValue("unique_name", out var name) ? name.GetString() : null;
        Role = claims.TryGetValue("role", out var role) ? role.GetString() : null;
        UserId = claims.TryGetValue("sub", out var sub) ? long.Parse(sub.GetString()!, CultureInfo.InvariantCulture) : 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        AccessToken = null;
        Username = null;
        Role = null;
        UserId = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static Dictionary<string, JsonElement> ReadClaims(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
    }
}
