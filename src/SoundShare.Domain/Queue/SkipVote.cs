namespace SoundShare.Domain.Queue;

public sealed class SkipVote
{
    private SkipVote()
    {
    }

    public SkipVote(long playlistTrackId, long userId, DateTime createdAt)
    {
        PlaylistTrackId = playlistTrackId;
        UserId = userId;
        CreatedAt = createdAt;
    }

    public long PlaylistTrackId { get; private set; }

    public long UserId { get; private set; }

    public DateTime CreatedAt { get; private set; }
}
