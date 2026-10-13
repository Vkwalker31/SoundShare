using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Domain.Catalog;

namespace SoundShare.Application.Services;

public sealed class CatalogService(ICatalogRepository catalog, ITrackSource trackSource, IUnitOfWork unitOfWork, TimeProvider time)
{
    public const int PageSize = 20;
    public const int MinLocalResults = 5;

    public async Task<TrackSearchResultDto> SearchAsync(string? query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 100)
        {
            throw new FluentValidation.ValidationException("Query must contain 1 to 100 characters.");
        }

        query = query.Trim();
        var local = await catalog.SearchAsync(query, PageSize, cancellationToken);
        if (local.Count >= MinLocalResults)
        {
            return new TrackSearchResultDto(local.Select(t => t.ToDto()).ToList(), true);
        }

        IReadOnlyList<ExternalTrack> external;
        try
        {
            external = await trackSource.SearchAsync(query, PageSize, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new TrackSearchResultDto(local.Select(t => t.ToDto()).ToList(), false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new TrackSearchResultDto(local.Select(t => t.ToDto()).ToList(), false);
        }

        var results = local.ToList();
        foreach (var found in external.Where(e => e.DurationSec > 0))
        {
            if (results.Any(t => t.ExternalSourceId == found.VideoId))
            {
                continue;
            }

            var track = await catalog.GetByExternalIdAsync(found.VideoId, cancellationToken) ?? await ImportAsync(found, cancellationToken);
            results.Add(track);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new TrackSearchResultDto(results.Take(PageSize).Select(t => t.ToDto()).ToList(), true);
    }

    private async Task<Track> ImportAsync(ExternalTrack found, CancellationToken cancellationToken)
    {
        var now = time.UtcNow();
        var (artists, title) = TitleParser.Parse(found.Title, found.ChannelTitle);
        var track = new Track(title.Length > 300 ? title[..300] : title, found.DurationSec, found.VideoId, now);
        foreach (var name in artists.Take(5))
        {
            var artist = await catalog.GetArtistByNameAsync(name, cancellationToken);
            if (artist is null)
            {
                artist = new Artist(name.Length > 200 ? name[..200] : name, now);
                catalog.AddArtist(artist);
            }

            track.AddArtist(artist);
        }

        catalog.AddTrack(track);
        return track;
    }
}
