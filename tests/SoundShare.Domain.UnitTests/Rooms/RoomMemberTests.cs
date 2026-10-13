using SoundShare.Domain.Common;
using SoundShare.Domain.Rooms;

namespace SoundShare.Domain.UnitTests.Rooms;

public class RoomMemberTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(RoomRole.Owner, RoomRole.Moderator, true)]
    [InlineData(RoomRole.Owner, RoomRole.Contributor, true)]
    [InlineData(RoomRole.Moderator, RoomRole.Contributor, true)]
    [InlineData(RoomRole.Moderator, RoomRole.Moderator, false)]
    [InlineData(RoomRole.Moderator, RoomRole.Owner, false)]
    [InlineData(RoomRole.Contributor, RoomRole.Contributor, false)]
    [InlineData(RoomRole.Contributor, RoomRole.Moderator, false)]
    public void CanModerate_RoleCombination_ReturnsExpected(RoomRole actor, RoomRole target, bool expected)
    {
        var result = RoomMember.CanModerate(actor, target);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Ban_ActiveMember_LeavesRoomAndIsBanned()
    {
        var member = new RoomMember(1, 2, RoomRole.Contributor, Now.AddHours(-1));

        member.Ban(Now, Now.AddMinutes(10));

        member.IsActive.ShouldBeFalse();
        member.LeftAt.ShouldBe(Now);
        member.IsBanned(Now.AddMinutes(5)).ShouldBeTrue();
        member.IsBanned(Now.AddMinutes(10)).ShouldBeFalse();
    }

    [Fact]
    public void Ban_UntilInPast_Throws()
    {
        var member = new RoomMember(1, 2, RoomRole.Contributor, Now.AddHours(-1));

        Should.Throw<DomainException>(() => member.Ban(Now, Now.AddMinutes(-1)));
    }

    [Fact]
    public void Rejoin_WhileBanned_Throws()
    {
        var member = new RoomMember(1, 2, RoomRole.Contributor, Now.AddHours(-1));
        member.Ban(Now, Now.AddMinutes(10));

        Should.Throw<DomainException>(() => member.Rejoin(Now.AddMinutes(1)));
    }

    [Fact]
    public void Rejoin_AfterBanExpired_ResetsMembership()
    {
        var member = new RoomMember(1, 2, RoomRole.Moderator, Now.AddHours(-1));
        member.Ban(Now, Now.AddMinutes(10));

        member.Rejoin(Now.AddMinutes(11));

        member.IsActive.ShouldBeTrue();
        member.BannedUntil.ShouldBeNull();
        member.Role.ShouldBe(RoomRole.Contributor);
        member.JoinedAt.ShouldBe(Now.AddMinutes(11));
    }

    [Fact]
    public void Leave_Twice_KeepsFirstLeaveTime()
    {
        var member = new RoomMember(1, 2, RoomRole.Contributor, Now.AddHours(-1));

        member.Leave(Now);
        member.Leave(Now.AddMinutes(5));

        member.LeftAt.ShouldBe(Now);
    }
}
