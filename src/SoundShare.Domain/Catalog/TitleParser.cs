namespace SoundShare.Domain.Catalog;

public static class TitleParser
{
    private static readonly string[] FeatMarkers = [" feat. ", " ft. ", " feat ", " ft ", " & "];

    public static (IReadOnlyList<string> Artists, string Title) Parse(string rawTitle, string channelName)
    {
        ArgumentNullException.ThrowIfNull(rawTitle);
        ArgumentNullException.ThrowIfNull(channelName);

        var separator = rawTitle.IndexOf(" - ", StringComparison.Ordinal);
        if (separator <= 0)
        {
            return ([CleanChannel(channelName)], rawTitle.Trim());
        }

        var artistPart = rawTitle[..separator];
        var title = rawTitle[(separator + 3)..].Trim();
        return (SplitArtists(artistPart), title);
    }

    private static List<string> SplitArtists(string artistPart)
    {
        var parts = new List<string> { artistPart };
        foreach (var marker in FeatMarkers)
        {
            parts = parts
                .SelectMany(p => p.Split(marker, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToList();
        }

        return parts.Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string CleanChannel(string channelName)
    {
        var trimmed = channelName.Trim();
        return trimmed.EndsWith(" - Topic", StringComparison.Ordinal) ? trimmed[..^8] : trimmed;
    }
}
