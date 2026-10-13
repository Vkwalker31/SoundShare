using SoundShare.Domain.Common;
using SoundShare.Domain.Queue;

namespace SoundShare.Domain.UnitTests.Queue;

public class QueueItemTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string> InvalidFromQueued => new() { "Finish", "Skip" };

    public static TheoryData<string> InvalidFromPlaying => new() { "StartPlaying", "Remove", "Pin" };

    [Fact]
    public void Constructor_NewItem_IsQueuedAndNotPinned()
    {
        var item = new QueueItem(1, 2, 3, Now);

        item.Status.ShouldBe(QueueItemStatus.Queued);
        item.IsPinned.ShouldBeFalse();
        item.StartedAt.ShouldBeNull();
    }

    [Fact]
    public void StartPlaying_PinnedQueuedItem_BecomesPlayingAndUnpinned()
    {
        var item = new QueueItem(1, 2, 3, Now);
        item.Pin(Now.AddMinutes(1));

        item.StartPlaying(Now.AddMinutes(2));

        item.Status.ShouldBe(QueueItemStatus.Playing);
        item.StartedAt.ShouldBe(Now.AddMinutes(2));
        item.IsPinned.ShouldBeFalse();
    }

    [Fact]
    public void Finish_PlayingItem_BecomesPlayed()
    {
        var item = Playing();

        item.Finish();

        item.Status.ShouldBe(QueueItemStatus.Played);
    }

    [Fact]
    public void Skip_PlayingItem_BecomesSkipped()
    {
        var item = Playing();

        item.Skip();

        item.Status.ShouldBe(QueueItemStatus.Skipped);
    }

    [Fact]
    public void Remove_QueuedItem_BecomesRemoved()
    {
        var item = new QueueItem(1, 2, 3, Now);

        item.Remove();

        item.Status.ShouldBe(QueueItemStatus.Removed);
    }

    [Theory]
    [MemberData(nameof(InvalidFromQueued))]
    public void Transition_NotAllowedFromQueued_Throws(string operation)
    {
        var item = new QueueItem(1, 2, 3, Now);

        Should.Throw<DomainException>(() => Apply(item, operation));
    }

    [Theory]
    [MemberData(nameof(InvalidFromPlaying))]
    public void Transition_NotAllowedFromPlaying_Throws(string operation)
    {
        var item = Playing();

        Should.Throw<DomainException>(() => Apply(item, operation));
    }

    [Theory]
    [InlineData(-5, false, true)]
    [InlineData(-6, false, true)]
    [InlineData(-4, false, false)]
    [InlineData(0, false, false)]
    [InlineData(-5, true, false)]
    public void TryAutoRemove_Score_RemovesOnlyUnpinnedAtThreshold(int score, bool pinned, bool expectedRemoved)
    {
        var item = new QueueItem(1, 2, 3, Now);
        if (pinned)
        {
            item.Pin(Now);
        }

        var removed = item.TryAutoRemove(score);

        removed.ShouldBe(expectedRemoved);
        item.Status.ShouldBe(expectedRemoved ? QueueItemStatus.AutoRemoved : QueueItemStatus.Queued);
    }

    [Fact]
    public void TryAutoRemove_PlayingItem_IsNotRemoved()
    {
        var item = Playing();

        item.TryAutoRemove(-10).ShouldBeFalse();
        item.Status.ShouldBe(QueueItemStatus.Playing);
    }

    [Fact]
    public void Pin_Twice_KeepsFirstPinTime()
    {
        var item = new QueueItem(1, 2, 3, Now);

        item.Pin(Now.AddMinutes(1));
        item.Pin(Now.AddMinutes(5));

        item.PinnedAt.ShouldBe(Now.AddMinutes(1));
    }

    private static QueueItem Playing()
    {
        var item = new QueueItem(1, 2, 3, Now);
        item.StartPlaying(Now);
        return item;
    }

    private static void Apply(QueueItem item, string operation)
    {
        switch (operation)
        {
            case "StartPlaying":
                item.StartPlaying(Now);
                break;
            case "Finish":
                item.Finish();
                break;
            case "Skip":
                item.Skip();
                break;
            case "Remove":
                item.Remove();
                break;
            case "Pin":
                item.Pin(Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }
}
