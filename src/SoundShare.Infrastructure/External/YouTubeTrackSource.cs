using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Xml;
using Microsoft.Extensions.Options;
using SoundShare.Application.Abstractions;

namespace SoundShare.Infrastructure.External;

public sealed class YouTubeTrackSource(HttpClient http, IOptions<YouTubeOptions> options) : ITrackSource
{
    private readonly YouTubeOptions _options = options.Value;

    public async Task<IReadOnlyList<ExternalTrack>> SearchAsync(string query, int take, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new HttpRequestException("YouTube API key is not configured.");
        }

        var key = Uri.EscapeDataString(_options.ApiKey);
        var searchUrl = $"search?part=snippet&type=video&videoCategoryId=10&maxResults={take.ToString(CultureInfo.InvariantCulture)}&q={Uri.EscapeDataString(query)}&key={key}";
        var search = await http.GetFromJsonAsync<SearchResponse>(searchUrl, cancellationToken);
        var ids = search?.Items?.Select(i => i.Id?.VideoId).OfType<string>().ToList() ?? [];
        if (ids.Count == 0)
        {
            return [];
        }

        var videosUrl = $"videos?part=snippet,contentDetails&id={string.Join(',', ids)}&key={key}";
        var videos = await http.GetFromJsonAsync<VideosResponse>(videosUrl, cancellationToken);
        return videos?.Items?
            .Where(v => v.Id is not null && v.Snippet?.Title is not null)
            .Select(v => new ExternalTrack(v.Id!, v.Snippet!.Title!, v.Snippet.ChannelTitle ?? string.Empty, ParseDuration(v.ContentDetails?.Duration)))
            .ToList() ?? [];
    }

    internal static int ParseDuration(string? isoDuration)
    {
        if (string.IsNullOrWhiteSpace(isoDuration))
        {
            return 0;
        }

        try
        {
            return (int)XmlConvert.ToTimeSpan(isoDuration).TotalSeconds;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private sealed record SearchResponse([property: JsonPropertyName("items")] IReadOnlyList<SearchItem>? Items);

    private sealed record SearchItem([property: JsonPropertyName("id")] SearchId? Id);

    private sealed record SearchId([property: JsonPropertyName("videoId")] string? VideoId);

    private sealed record VideosResponse([property: JsonPropertyName("items")] IReadOnlyList<VideoItem>? Items);

    private sealed record VideoItem(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] Snippet? Snippet,
        [property: JsonPropertyName("contentDetails")] ContentDetails? ContentDetails);

    private sealed record Snippet(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("channelTitle")] string? ChannelTitle);

    private sealed record ContentDetails([property: JsonPropertyName("duration")] string? Duration);
}
