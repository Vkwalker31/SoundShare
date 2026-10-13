namespace SoundShare.Domain.Queue;

public static class QueueRules
{
    public const int MaxActiveItemsPerUser = 3;
    public const int AutoRemoveThreshold = -5;
    public const int MaxRoomMembers = 100;

    public static bool IsValidVote(int value) => value is -1 or 0 or 1;

    public static bool ShouldAutoRemove(int score, bool isPinned) => !isPinned && score <= AutoRemoveThreshold;

    public static int RequiredSkipVotes(int onlineMembers) => (onlineMembers / 2) + 1;

    public static bool IsSkipReached(int skipVotes, int onlineMembers) =>
        onlineMembers > 0 && skipVotes * 2 > onlineMembers;

    public static IReadOnlyList<T> Order<T>(IEnumerable<T> items, Func<T, DateTime?> pinnedAt, Func<T, int> score, Func<T, DateTime> addedAt) =>
        items
            .OrderBy(i => pinnedAt(i) is null)
            .ThenBy(pinnedAt)
            .ThenByDescending(score)
            .ThenBy(addedAt)
            .ToList();
}
