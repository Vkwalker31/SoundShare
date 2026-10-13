using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Domain.Users;

namespace SoundShare.Application.Services;

public sealed class AuthService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    TimeProvider time,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator)
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureUniqueAsync(users, request.Username, request.Email, cancellationToken);

        var user = new User(request.Username, request.Email, passwordHasher.Hash(request.Password), SystemRole.User, time.UtcNow());
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToDto();
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await users.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new AuthenticationFailedException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new AccountBlockedException("The account is blocked.");
        }

        return await IssueTokensAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthenticationFailedException("Refresh token is missing.");
        }

        var now = time.UtcNow();
        var stored = await users.GetRefreshTokenAsync(tokens.HashRefreshToken(refreshToken), cancellationToken)
            ?? throw new AuthenticationFailedException("Refresh token is invalid.");

        if (stored.IsRevoked)
        {
            await RevokeAllAsync(stored.UserId, now, cancellationToken);
            throw new AuthenticationFailedException("Refresh token reuse detected.");
        }

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken);
        if (!stored.IsActive(now) || user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException("Refresh token is expired.");
        }

        return await unitOfWork.InTransactionAsync(
            async ct =>
            {
                var result = await IssueTokensAsync(user, ipAddress, ct);
                var replacement = await users.GetRefreshTokenAsync(tokens.HashRefreshToken(result.RefreshToken), ct);
                stored.Revoke(now, replacement?.Id);
                await unitOfWork.SaveChangesAsync(ct);
                return result;
            },
            cancellationToken);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var stored = await users.GetRefreshTokenAsync(tokens.HashRefreshToken(refreshToken), cancellationToken);
        if (stored is null)
        {
            return;
        }

        stored.Revoke(time.UtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task LogoutAllAsync(long userId, CancellationToken cancellationToken) =>
        RevokeAllAsync(userId, time.UtcNow(), cancellationToken);

    internal static async Task EnsureUniqueAsync(IUserRepository users, string username, string email, CancellationToken cancellationToken)
    {
        if (await users.UsernameExistsAsync(username, cancellationToken))
        {
            throw new ConflictException("Username is already taken.");
        }

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("Email is already in use.");
        }
    }

    private async Task<AuthResult> IssueTokensAsync(User user, string? ipAddress, CancellationToken cancellationToken)
    {
        var now = time.UtcNow();
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.GenerateRefreshToken();
        var expiresAt = now.Add(tokens.RefreshTokenLifetime);
        users.AddRefreshToken(new RefreshToken(user.Id, refresh.Hash, now, expiresAt, ipAddress));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AuthResult(access.Token, access.ExpiresIn, refresh.Value, expiresAt);
    }

    private async Task RevokeAllAsync(long userId, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var token in await users.GetActiveRefreshTokensAsync(userId, now, cancellationToken))
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
