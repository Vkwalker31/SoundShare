namespace SoundShare.Application.Common;

internal static class Clock
{
    public static DateTime UtcNow(this TimeProvider timeProvider) => timeProvider.GetUtcNow().UtcDateTime;
}
