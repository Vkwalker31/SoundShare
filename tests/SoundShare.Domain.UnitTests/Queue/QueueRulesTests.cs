using SoundShare.Domain.Queue;

namespace SoundShare.Domain.UnitTests.Queue;

public class QueueRulesTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(-2, false)]
    public void IsValidVote_Value_ReturnsExpected(int value, bool expected)
    {
        QueueRules.IsValidVote(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(2, 4, false)]
    [InlineData(3, 4, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 3, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 0, false)]
    public void IsSkipReached_StrictlyMoreThanHalf_ReturnsExpected(int votes, int online, bool expected)
    {
        QueueRules.IsSkipReached(votes, online).ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(100, 51)]
    public void RequiredSkipVotes_OnlineMembers_ReturnsMajority(int online, int expected)
    {
        QueueRules.RequiredSkipVotes(online).ShouldBe(expected);
    }

    [Fact]
    public void Order_MixedItems_PinnedFirstThenScoreThenAddedTime()
    {
        var t0 = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            (Name: "late-high", Pinned: (DateTime?)null, Score: 5, Added: t0.AddMinutes(3)),
            (Name: "pinned-second", Pinned: (DateTime?)t0.AddMinutes(9), Score: -1, Added: t0.AddMinutes(1)),
            (Name: "early-equal", Pinned: (DateTime?)null, Score: 2, Added: t0.AddMinutes(1)),
            (Name: "pinned-first", Pinned: (DateTime?)t0.AddMinutes(8), Score: 0, Added: t0.AddMinutes(2)),
            (Name: "late-equal", Pinned: (DateTime?)null, Score: 2, Added: t0.AddMinutes(4)),
        };

        var ordered = QueueRules.Order(items, i => i.Pinned, i => i.Score, i => i.Added);

        ordered.Select(i => i.Name).ShouldBe(["pinned-first", "pinned-second", "late-high", "early-equal", "late-equal"]);
    }
}
