namespace SoundShare.Domain.Queue;

public sealed class TrackVote
{
    private TrackVote()
    {
    }

    public TrackVote(long playlistTrackId, long userId, int value, DateTime votedAt)
    {
        PlaylistTrackId = playlistTrackId;
        UserId = userId;
        VoteValue = value;
        VotedAt = votedAt;
    }

    public long PlaylistTrackId { get; private set; }

    public long UserId { get; private set; }

    public int VoteValue { get; private set; }

    public DateTime VotedAt { get; private set; }

    public void Change(int value, DateTime now)
    {
        VoteValue = value;
        VotedAt = now;
    }
}
