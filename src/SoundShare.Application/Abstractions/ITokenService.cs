using SoundShare.Domain.Users;

namespace SoundShare.Application.Abstractions;

public sealed record AccessToken(string Token, int ExpiresIn);

public sealed record GeneratedRefreshToken(string Value, byte[] Hash);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    GeneratedRefreshToken GenerateRefreshToken();

    byte[] HashRefreshToken(string value);

    TimeSpan RefreshTokenLifetime { get; }
}
