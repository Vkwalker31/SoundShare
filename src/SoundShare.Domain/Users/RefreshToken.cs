namespace SoundShare.Domain.Users;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(long userId, byte[] tokenHash, DateTime createdAt, DateTime expiresAt, string? createdByIp)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    public long Id { get; private set; }

    public long UserId { get; private set; }

    public byte[] TokenHash { get; private set; } = [];

    public DateTime ExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public long? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTime now, long? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now < CreatedAt ? CreatedAt : now;
        ReplacedByTokenId = replacedByTokenId;
    }
}
