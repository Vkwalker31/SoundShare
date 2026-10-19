using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Services;
using SoundShare.Domain.Catalog;

namespace SoundShare.Application.UnitTests;

public class CatalogServiceTests
{
    private readonly ICatalogRepository _catalog = Substitute.For<ICatalogRepository>();
    private readonly ITrackSource _source = Substitute.For<ITrackSource>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CatalogService _service;

    public CatalogServiceTests()
    {
        _service = new CatalogService(_catalog, _source, _unitOfWork, TestClock.Fixed());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_EmptyQuery_ThrowsValidation(string? query)
    {
        await Should.ThrowAsync<ValidationException>(() => _service.SearchAsync(query, default));
    }

    [Fact]
    public async Task Search_QueryLongerThan100Chars_ThrowsValidation()
    {
        await Should.ThrowAsync<ValidationException>(() => _service.SearchAsync(new string('a', 101), default));
    }

    [Fact]
    public async Task Search_EnoughLocalResults_DoesNotCallYouTube()
    {
        _catalog.SearchAsync("rock", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns(LocalTracks(5));

        var result = await _service.SearchAsync(" rock ", default);

        result.Items.Count.ShouldBe(5);
        result.ExternalSearchAvailable.ShouldBeTrue();
        await _source.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_YouTubeUnavailable_ReturnsLocalResultsWithFlag()
    {
        _catalog.SearchAsync("rock", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns(LocalTracks(2));
        _source.SearchAsync("rock", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<ExternalTrack>>(_ => throw new HttpRequestException("quota"));

        var result = await _service.SearchAsync("rock", default);

        result.Items.Count.ShouldBe(2);
        result.ExternalSearchAvailable.ShouldBeFalse();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Search_YouTubeTimeout_ReturnsLocalResultsWithFlag()
    {
        _catalog.SearchAsync("rock", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns(LocalTracks(1));
        _source.SearchAsync("rock", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<ExternalTrack>>(_ => throw new TaskCanceledException());

        var result = await _service.SearchAsync("rock", default);

        result.ExternalSearchAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Search_NewYouTubeResults_ImportsTracksWithParsedArtists()
    {
        _catalog.SearchAsync("rick", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns([]);
        _source.SearchAsync("rick", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns(
        [
            new ExternalTrack("dQw4w9WgXcQ", "Rick Astley - Never Gonna Give You Up", "RickAstleyVEVO", 213),
            new ExternalTrack("live0000001", "24/7 radio", "Channel", 0),
        ]);

        var result = await _service.SearchAsync("rick", default);

        var track = result.Items.ShouldHaveSingleItem();
        track.Title.ShouldBe("Never Gonna Give You Up");
        track.Artists.ShouldBe(["Rick Astley"]);
        _catalog.Received(1).AddTrack(Arg.Is<Track>(t => t.ExternalSourceId == "dQw4w9WgXcQ" && t.DurationSec == 213));
        _catalog.Received(1).AddArtist(Arg.Is<Artist>(a => a.Name == "Rick Astley"));
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Search_KnownArtistAndTrack_ReusesExistingRecords()
    {
        var existing = new Track("Numb", 187, "kXYiU_JCYtU", TestClock.Now).WithId(40);
        _catalog.SearchAsync("numb", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns([]);
        _source.SearchAsync("numb", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns(
        [
            new ExternalTrack("kXYiU_JCYtU", "Numb", "Linkin Park", 187),
            new ExternalTrack("eVTXPUF4Oz4", "In The End", "Linkin Park", 216),
        ]);
        _catalog.GetByExternalIdAsync("kXYiU_JCYtU", Arg.Any<CancellationToken>()).Returns(existing);
        _catalog.GetArtistByNameAsync("Linkin Park", Arg.Any<CancellationToken>()).Returns(new Artist("Linkin Park", TestClock.Now).WithId(9));

        var result = await _service.SearchAsync("numb", default);

        result.Items.Count.ShouldBe(2);
        _catalog.Received(1).AddTrack(Arg.Is<Track>(t => t.ExternalSourceId == "eVTXPUF4Oz4"));
        _catalog.DidNotReceive().AddArtist(Arg.Any<Artist>());
    }

    [Fact]
    public async Task Search_DuplicateOfLocalResult_IsSkipped()
    {
        var local = new Track("Numb", 187, "kXYiU_JCYtU", TestClock.Now).WithId(40);
        _catalog.SearchAsync("numb", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns([local]);
        _source.SearchAsync("numb", CatalogService.PageSize, Arg.Any<CancellationToken>()).Returns([new ExternalTrack("kXYiU_JCYtU", "Numb", "Linkin Park", 187)]);

        var result = await _service.SearchAsync("numb", default);

        result.Items.ShouldHaveSingleItem().Id.ShouldBe(40);
        _catalog.DidNotReceive().AddTrack(Arg.Any<Track>());
    }

    private static List<Track> LocalTracks(int count) =>
        Enumerable.Range(1, count).Select(i => new Track($"Song {i}", 180, $"local{i:D6}", TestClock.Now).WithId(i)).ToList();
}
