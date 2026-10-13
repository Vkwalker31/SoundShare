namespace SoundShare.Domain.Catalog;

public sealed class TrackArtist
{
    private TrackArtist()
    {
    }

    internal TrackArtist(Track track, Artist artist, int artistOrder)
    {
        Track = track;
        Artist = artist;
        ArtistOrder = artistOrder;
    }

    public long TrackId { get; private set; }

    public long ArtistId { get; private set; }

    public int ArtistOrder { get; private set; }

    public Track Track { get; private set; } = null!;

    public Artist Artist { get; private set; } = null!;
}
