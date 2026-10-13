using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SoundShare.Application.Abstractions;
using SoundShare.Domain.Users;

namespace SoundShare.Infrastructure.Security;

internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider time) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = time.GetUtcNow().UtcDateTime;
        var lifetime = TimeSpan.FromMinutes(_options.AccessTokenMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim("role", user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(_options), SecurityAlgorithms.HmacSha256),
        };
        return new AccessToken(_handler.CreateToken(descriptor), (int)lifetime.TotalSeconds);
    }

    public GeneratedRefreshToken GenerateRefreshToken()
    {
        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new GeneratedRefreshToken(value, HashRefreshToken(value));
    }

    public byte[] HashRefreshToken(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    public static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.SigningKey));
}
