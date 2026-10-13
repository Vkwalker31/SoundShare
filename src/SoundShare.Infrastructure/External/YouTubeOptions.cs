namespace SoundShare.Infrastructure.External;

public sealed class YouTubeOptions
{
    public const string SectionName = "YouTube";

    public string ApiKey { get; set; } = string.Empty;

    public Uri BaseUrl { get; set; } = new("https://www.googleapis.com/youtube/v3/");
}
