namespace SoundShare.Domain.Catalog;

public sealed class Artist
{
    private Artist()
    {
    }

    public Artist(string name, DateTime createdAt)
    {
        Name = name;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public string Name { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
}
