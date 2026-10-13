namespace SoundShare.Application.Abstractions;

public sealed record ExternalTrack(string VideoId, string Title, string ChannelTitle, int DurationSec);

public interface ITrackSource
{
    Task<IReadOnlyList<ExternalTrack>> SearchAsync(string query, int take, CancellationToken cancellationToken);
}
