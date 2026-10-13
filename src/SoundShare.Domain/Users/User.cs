namespace SoundShare.Domain.Users;

public sealed class User
{
    private User()
    {
    }

    public User(string username, string email, string passwordHash, SystemRole role, DateTime createdAt)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public string Username { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public SystemRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void Block() => IsActive = false;

    public void Unblock() => IsActive = true;

    public void ChangeRole(SystemRole role) => Role = role;

    public void Anonymize(string unusablePasswordHash)
    {
        Username = $"deleted_{Id}";
        Email = $"deleted_{Id}@deleted.invalid";
        PasswordHash = unusablePasswordHash;
        IsActive = false;
    }
}
