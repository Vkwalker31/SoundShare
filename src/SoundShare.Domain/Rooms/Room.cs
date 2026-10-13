namespace SoundShare.Domain.Rooms;

public sealed class Room
{
    private Room()
    {
    }

    public Room(string name, long createdById, bool isPrivate, InviteCode inviteCode, DateTime createdAt)
    {
        Name = name;
        CreatedById = createdById;
        IsPrivate = isPrivate;
        InviteCode = inviteCode.Value;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public string Name { get; private set; } = null!;

    public long CreatedById { get; private set; }

    public bool IsPrivate { get; private set; }

    public string InviteCode { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTime? EmptySince { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void Close() => IsActive = false;

    public void MarkEmpty(DateTime now) => EmptySince ??= now;

    public void MarkOccupied() => EmptySince = null;
}
