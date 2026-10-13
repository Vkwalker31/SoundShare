namespace SoundShare.Domain.Queue;

public sealed class Playlist
{
    private Playlist()
    {
    }

    private Playlist(string name, long? roomId, long? ownerId, DateTime createdAt)
    {
        Name = name;
        RoomId = roomId;
        OwnerId = ownerId;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public string Name { get; private set; } = null!;

    public long? RoomId { get; private set; }

    public long? OwnerId { get; private set; }

    public long QueueVersion { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static Playlist ForRoom(long roomId, string roomName, DateTime createdAt) =>
        new($"Queue: {roomName}", roomId, null, createdAt);

    public static Playlist ForUser(long ownerId, string name, DateTime createdAt) =>
        new(name, null, ownerId, createdAt);

    public void IncrementVersion() => QueueVersion++;
}
