using SoundShare.Domain.Catalog;

namespace SoundShare.Domain.UnitTests.Catalog;

public class TitleParserTests
{
    [Theory]
    [InlineData("Luis Fonsi - Despacito ft. Daddy Yankee", "Channel", "Luis Fonsi", "Despacito ft. Daddy Yankee")]
    [InlineData("Rick Astley - Never Gonna Give You Up", "RickAstleyVEVO", "Rick Astley", "Never Gonna Give You Up")]
    [InlineData("Thunderstruck", "AC/DC - Topic", "AC/DC", "Thunderstruck")]
    [InlineData("Numb", "Linkin Park", "Linkin Park", "Numb")]
    public void Parse_Title_ExtractsPrimaryArtistAndTitle(string raw, string channel, string firstArtist, string title)
    {
        var (artists, parsedTitle) = TitleParser.Parse(raw, channel);

        artists[0].ShouldBe(firstArtist);
        parsedTitle.ShouldBe(title);
    }

    [Fact]
    public void Parse_FeaturedArtistsBeforeDash_ReturnsAllInOrder()
    {
        var (artists, title) = TitleParser.Parse("Artist1 feat. Artist2 & Artist3 - Song", "Channel");

        artists.ShouldBe(["Artist1", "Artist2", "Artist3"]);
        title.ShouldBe("Song");
    }

    [Fact]
    public void Track_AddSameArtistTwice_KeepsSingleOrderedEntry()
    {
        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var track = new Track("Despacito", 282, "kJQP7kiw5Fk", now);
        var fonsi = new Artist("Luis Fonsi", now);
        var yankee = new Artist("Daddy Yankee", now);

        track.AddArtist(fonsi);
        track.AddArtist(yankee);
        track.AddArtist(fonsi);

        track.Artists.Select(a => (a.Artist.Name, a.ArtistOrder)).ShouldBe([("Luis Fonsi", 1), ("Daddy Yankee", 2)]);
        track.ThumbnailUrl.ToString().ShouldBe("https://i.ytimg.com/vi/kJQP7kiw5Fk/hqdefault.jpg");
    }
}
