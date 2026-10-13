using Microsoft.AspNetCore.Identity;
using SoundShare.Domain.Users;

namespace SoundShare.Infrastructure.Security;

internal sealed class IdentityPasswordHasher : Application.Abstractions.IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string hash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
