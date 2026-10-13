using SoundShare.Domain.Common;

namespace SoundShare.Domain.Rooms;

public sealed class RoomMember
{
    private RoomMember()
    {
    }

    public RoomMember(long roomId, long userId, RoomRole role, DateTime joinedAt)
    {
        RoomId = roomId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public long RoomId { get; private set; }

    public long UserId { get; private set; }

    public RoomRole Role { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public DateTime? LeftAt { get; private set; }

    public DateTime? BannedUntil { get; private set; }

    public bool IsActive => LeftAt is null;

    public static bool CanModerate(RoomRole actor, RoomRole target) => actor > target && actor >= RoomRole.Moderator;

    public bool IsBanned(DateTime now) => BannedUntil is not null && BannedUntil > now;

    public void Leave(DateTime now)
    {
        if (LeftAt is null)
        {
            LeftAt = now < JoinedAt ? JoinedAt : now;
        }
    }

    public void Rejoin(DateTime now)
    {
        if (IsBanned(now))
        {
            throw new DomainException($"Banned until {BannedUntil:O}.");
        }

        LeftAt = null;
        BannedUntil = null;
        JoinedAt = now;
        Role = RoomRole.Contributor;
    }

    public void Ban(DateTime now, DateTime until)
    {
        if (until <= now)
        {
            throw new DomainException("Ban must end in the future.");
        }

        Leave(now);
        BannedUntil = until;
    }

    public void ChangeRole(RoomRole role) => Role = role;
}
