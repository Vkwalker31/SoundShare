using System.Net;
using Microsoft.Extensions.Options;
using SoundShare.Infrastructure.External;
using SoundShare.IntegrationTests.Infrastructure;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace SoundShare.IntegrationTests.External;

[Collection(SoundShareTestGroup.Name)]
public class YouTubeTrackSourceTests(SoundShareFixture fixture)
{
    [Fact]
    public async Task Search_StubReturnsVideos_MapsTitleChannelAndDuration()
    {
        var query = TestApi.UniqueName("yt");
        var id = TestApi.NewVideoId();
        fixture.StubYouTube(query, (id, "Rick Astley - Never Gonna Give You Up", "RickAstleyVEVO", "PT3M33S"));

        var tracks = await CreateSource().SearchAsync(query, 20, default);

        var track = tracks.ShouldHaveSingleItem();
        track.VideoId.ShouldBe(id);
        track.Title.ShouldBe("Rick Astley - Never Gonna Give You Up");
        track.ChannelTitle.ShouldBe("RickAstleyVEVO");
        track.DurationSec.ShouldBe(213);
    }

    [Fact]
    public async Task Search_NoVideosFound_ReturnsEmptyList()
    {
        var query = TestApi.UniqueName("none");
        fixture.YouTube
            .Given(Request.Create().WithPath("/youtube/v3/search").WithParam("q", query).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { items = Array.Empty<object>() }));

        var tracks = await CreateSource().SearchAsync(query, 20, default);

        tracks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_QuotaExceeded_ThrowsHttpRequestException()
    {
        var query = TestApi.UniqueName("quota");
        fixture.YouTube
            .Given(Request.Create().WithPath("/youtube/v3/search").WithParam("q", query).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Forbidden).WithBodyAsJson(new { error = new { code = 403, message = "quotaExceeded" } }));

        var error = await Should.ThrowAsync<HttpRequestException>(() => CreateSource().SearchAsync(query, 20, default));

        error.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Search_SlowResponse_TimesOut()
    {
        var query = TestApi.UniqueName("slow");
        fixture.YouTube
            .Given(Request.Create().WithPath("/youtube/v3/search").WithParam("q", query).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { items = Array.Empty<object>() }).WithDelay(TimeSpan.FromSeconds(3)));

        await Should.ThrowAsync<TaskCanceledException>(() => CreateSource(TimeSpan.FromSeconds(1)).SearchAsync(query, 20, default));
    }

    [Fact]
    public async Task Search_MissingApiKey_ThrowsWithoutCallingYouTube()
    {
        using var http = new HttpClient { BaseAddress = new Uri($"{fixture.YouTube.Url}/youtube/v3/") };
        var source = new YouTubeTrackSource(http, Options.Create(new YouTubeOptions { ApiKey = string.Empty }));

        await Should.ThrowAsync<HttpRequestException>(() => source.SearchAsync("anything", 20, default));
    }

    private YouTubeTrackSource CreateSource(TimeSpan? timeout = null)
    {
        var http = new HttpClient { BaseAddress = new Uri($"{fixture.YouTube.Url}/youtube/v3/"), Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        return new YouTubeTrackSource(http, Options.Create(new YouTubeOptions { ApiKey = "test-key" }));
    }
}
