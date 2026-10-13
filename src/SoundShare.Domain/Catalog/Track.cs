namespace SoundShare.Domain.Catalog;

public sealed class Track
{
    public const string YouTubeSource = "YOUTUBE";

    private readonly List<TrackArtist> _artists = [];

    private Track()
    {
    }

    public Track(string title, int durationSec, string externalSourceId, DateTime createdAt)
    {
        Title = title;
        DurationSec = durationSec;
        ExternalSource = YouTubeSource;
        ExternalSourceId = externalSourceId;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public string Title { get; private set; } = null!;

    public int DurationSec { get; private set; }

    public string ExternalSource { get; private set; } = null!;

    public string ExternalSourceId { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<TrackArtist> Artists => _artists;

    public Uri ThumbnailUrl => new($"https://i.ytimg.com/vi/{ExternalSourceId}/hqdefault.jpg");

    public void AddArtist(Artist artist)
    {
        if (_artists.Any(a => a.Artist == artist))
        {
            return;
        }

        _artists.Add(new TrackArtist(this, artist, _artists.Count + 1));
    }
}
