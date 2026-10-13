using SoundShare.Domain.Catalog;

namespace SoundShare.Application.Abstractions;

public interface ICatalogRepository
{
    Task<Track?> GetTrackAsync(long trackId, CancellationToken cancellationToken);

    Task<Track?> GetByExternalIdAsync(string externalSourceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Track>> SearchAsync(string query, int take, CancellationToken cancellationToken);

    Task<Artist?> GetArtistByNameAsync(string name, CancellationToken cancellationToken);

    void AddTrack(Track track);

    void AddArtist(Artist artist);
}
