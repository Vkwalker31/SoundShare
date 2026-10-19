using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.Application.Validation;
using SoundShare.Domain.Queue;
using SoundShare.Domain.Rooms;

namespace SoundShare.Application.UnitTests;

public class RoomServiceTests
{
    private const long RoomId = 10;
    private const long UserId = 7;

    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly IQueueRepository _queues = Substitute.For<IQueueRepository>();
    private readonly IRoomNotifier _notifier = Substitute.For<IRoomNotifier>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RoomService _service;

    public RoomServiceTests()
    {
        _service = new RoomService(_rooms, _queues, _unitOfWork, TestClock.Fixed(), _notifier,
            new CreateRoomRequestValidator(), new JoinByCodeRequestValidator(), new BanRequestValidator());
    }

    [Fact]
    public async Task Create_ValidRequest_CreatesRoomQueueAndOwnerMembership()
    {
        var result = await _service.CreateAsync(UserId, new CreateRoomRequest("  Friday Rock  ", false), default);

        result.Name.ShouldBe("Friday Rock");
        InviteCode.TryParse(result.InviteCode, out _).ShouldBeTrue();
        _rooms.Received(1).Add(Arg.Is<Room>(r => r.Name == "Friday Rock" && r.CreatedById == UserId));
        _queues.Received(1).AddPlaylist(Arg.Any<Playlist>());
        _rooms.Received(1).AddMember(Arg.Is<RoomMember>(m => m.UserId == UserId && m.Role == RoomRole.Owner));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_BlankName_ThrowsValidation(string name)
    {
        await Should.ThrowAsync<ValidationException>(() => _service.CreateAsync(UserId, new CreateRoomRequest(name, false), default));

        _rooms.DidNotReceive().Add(Arg.Any<Room>());
    }

    [Fact]
    public async Task Create_UserInAnotherRoom_LeavesPreviousRoom()
    {
        var previous = new RoomMember(20, UserId, RoomRole.Contributor, TestClock.Now.AddHours(-1));
        _rooms.GetActiveMembershipAsync(UserId, Arg.Any<CancellationToken>()).Returns(previous);
        _rooms.GetActiveMembersAsync(20, Arg.Any<CancellationToken>()).Returns([]);

        await _service.CreateAsync(UserId, new CreateRoomRequest("New room", true), default);

        previous.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task GetState_PrivateRoomForNonMember_ThrowsNotFound()
    {
        GivenRoom(isPrivate: true);

        await Should.ThrowAsync<NotFoundException>(() => _service.GetStateAsync(RoomId, UserId, default));
    }

    [Fact]
    public async Task GetState_PublicRoomForNonMember_HidesInviteCode()
    {
        GivenRoom(isPrivate: false);
        _rooms.CountActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns(4);

        var state = await _service.GetStateAsync(RoomId, UserId, default);

        state.MyRole.ShouldBe("None");
        state.InviteCode.ShouldBeNull();
        state.MembersCount.ShouldBe(4);
    }

    [Fact]
    public async Task GetState_Member_SeesInviteCodeAndRole()
    {
        var room = GivenRoom(isPrivate: true);
        GivenMember(UserId, RoomRole.Moderator);

        var state = await _service.GetStateAsync(RoomId, UserId, default);

        state.MyRole.ShouldBe("Moderator");
        state.InviteCode.ShouldBe(room.InviteCode);
    }

    [Fact]
    public async Task JoinPublic_PrivateRoom_ThrowsNotFound()
    {
        GivenRoom(isPrivate: true);

        await Should.ThrowAsync<NotFoundException>(() => _service.JoinPublicAsync(RoomId, UserId, default));
    }

    [Fact]
    public async Task JoinPublic_ClosedRoom_ThrowsNotFound()
    {
        GivenRoom(isPrivate: false).Close();

        await Should.ThrowAsync<NotFoundException>(() => _service.JoinPublicAsync(RoomId, UserId, default));
    }

    [Fact]
    public async Task JoinPublic_NewUser_AddsContributorAndNotifies()
    {
        GivenRoom(isPrivate: false);
        _rooms.CountActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns(5);
        _rooms.When(r => r.AddMember(Arg.Any<RoomMember>()))
            .Do(call => _rooms.GetMemberAsync(RoomId, UserId, Arg.Any<CancellationToken>()).Returns(call.Arg<RoomMember>()));

        var state = await _service.JoinPublicAsync(RoomId, UserId, default);

        state.MyRole.ShouldBe("Contributor");
        await _rooms.Received(1).LockAsync(RoomId, Arg.Any<CancellationToken>());
        await _notifier.Received(1).QueueChangedAsync(RoomId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinPublic_RoomFull_ThrowsConflict()
    {
        GivenRoom(isPrivate: false);
        _rooms.CountActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns(QueueRules.MaxRoomMembers);

        await Should.ThrowAsync<ConflictException>(() => _service.JoinPublicAsync(RoomId, UserId, default));

        _rooms.DidNotReceive().AddMember(Arg.Any<RoomMember>());
    }

    [Fact]
    public async Task JoinPublic_BannedUser_ThrowsForbidden()
    {
        GivenRoom(isPrivate: false);
        var member = GivenMember(UserId, RoomRole.Contributor);
        member.Ban(TestClock.Now.AddMinutes(-1), TestClock.Now.AddHours(1));

        await Should.ThrowAsync<ForbiddenException>(() => _service.JoinPublicAsync(RoomId, UserId, default));
    }

    [Fact]
    public async Task JoinPublic_BanExpired_RejoinsAsContributor()
    {
        GivenRoom(isPrivate: false);
        var member = GivenMember(UserId, RoomRole.Moderator);
        member.Ban(TestClock.Now.AddHours(-2), TestClock.Now.AddHours(-1));

        await _service.JoinPublicAsync(RoomId, UserId, default);

        member.IsActive.ShouldBeTrue();
        member.Role.ShouldBe(RoomRole.Contributor);
    }

    [Theory]
    [InlineData("0O1I00")]
    [InlineData("ABC")]
    [InlineData("ABCDEFG")]
    public async Task JoinByCode_MalformedCode_ThrowsNotFound(string code)
    {
        await Should.ThrowAsync<NotFoundException>(() => _service.JoinByCodeAsync(UserId, new JoinByCodeRequest(code), default));

        await _rooms.DidNotReceive().GetByInviteCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leave_Owner_TransfersOwnershipToModerator()
    {
        GivenRoom(isPrivate: false);
        var owner = GivenMember(UserId, RoomRole.Owner);
        var contributor = new RoomMember(RoomId, 8, RoomRole.Contributor, TestClock.Now.AddHours(-3));
        var moderator = new RoomMember(RoomId, 9, RoomRole.Moderator, TestClock.Now.AddHours(-1));
        _rooms.GetActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns([contributor, moderator]);

        await _service.LeaveAsync(RoomId, UserId, default);

        owner.IsActive.ShouldBeFalse();
        owner.Role.ShouldBe(RoomRole.Contributor);
        moderator.Role.ShouldBe(RoomRole.Owner);
        contributor.Role.ShouldBe(RoomRole.Contributor);
    }

    [Fact]
    public async Task Leave_LastMember_MarksRoomEmpty()
    {
        var room = GivenRoom(isPrivate: false);
        GivenMember(UserId, RoomRole.Owner);
        _rooms.GetActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns([]);

        await _service.LeaveAsync(RoomId, UserId, default);

        room.EmptySince.ShouldBe(TestClock.Now);
    }

    [Fact]
    public async Task Leave_NotMember_ThrowsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => _service.LeaveAsync(RoomId, UserId, default));
    }

    [Theory]
    [InlineData(RoomRole.Moderator, RoomRole.Owner)]
    [InlineData(RoomRole.Moderator, RoomRole.Moderator)]
    [InlineData(RoomRole.Contributor, RoomRole.Contributor)]
    public async Task Ban_TargetWithEqualOrHigherRole_ThrowsForbidden(RoomRole actorRole, RoomRole targetRole)
    {
        GivenRoom(isPrivate: false);
        GivenMember(UserId, actorRole);
        var target = GivenMember(8, targetRole);

        await Should.ThrowAsync<ForbiddenException>(() => _service.BanAsync(RoomId, UserId, new BanRequest(8, 60), default));

        target.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Ban_OwnerBansContributorForHour_RemovesMemberUntilBanEnds()
    {
        GivenRoom(isPrivate: false);
        GivenMember(UserId, RoomRole.Owner);
        var target = GivenMember(8, RoomRole.Contributor);

        await _service.BanAsync(RoomId, UserId, new BanRequest(8, 60), default);

        target.IsActive.ShouldBeFalse();
        target.BannedUntil.ShouldBe(TestClock.Now.AddMinutes(60));
    }

    [Fact]
    public async Task Ban_WithoutDuration_BansPermanently()
    {
        GivenRoom(isPrivate: false);
        GivenMember(UserId, RoomRole.Moderator);
        var target = GivenMember(8, RoomRole.Contributor);

        await _service.BanAsync(RoomId, UserId, new BanRequest(8, null), default);

        target.BannedUntil!.Value.Year.ShouldBe(9999);
    }

    [Fact]
    public async Task ChangeMemberRole_ToOwner_TransfersOwnership()
    {
        GivenRoom(isPrivate: false);
        var owner = GivenMember(UserId, RoomRole.Owner);
        var target = GivenMember(8, RoomRole.Contributor);

        await _service.ChangeMemberRoleAsync(RoomId, UserId, 8, new ChangeMemberRoleRequest("Owner"), default);

        target.Role.ShouldBe(RoomRole.Owner);
        owner.Role.ShouldBe(RoomRole.Moderator);
    }

    [Fact]
    public async Task ChangeMemberRole_ByModerator_ThrowsForbidden()
    {
        GivenRoom(isPrivate: false);
        GivenMember(UserId, RoomRole.Moderator);

        await Should.ThrowAsync<ForbiddenException>(() =>
            _service.ChangeMemberRoleAsync(RoomId, UserId, 8, new ChangeMemberRoleRequest("Moderator"), default));
    }

    [Fact]
    public async Task Close_ByContributor_ThrowsForbidden()
    {
        var room = GivenRoom(isPrivate: false);
        GivenMember(UserId, RoomRole.Contributor);

        await Should.ThrowAsync<ForbiddenException>(() => _service.CloseAsync(RoomId, UserId, false, default));

        room.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Close_ByAdmin_ClosesRoomAndRemovesMembers()
    {
        var room = GivenRoom(isPrivate: false);
        var member = new RoomMember(RoomId, 8, RoomRole.Owner, TestClock.Now.AddHours(-1));
        _rooms.GetActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns([member]);

        await _service.CloseAsync(RoomId, 1, true, default);

        room.IsActive.ShouldBeFalse();
        member.IsActive.ShouldBeFalse();
        await _notifier.Received(1).RoomClosedAsync(RoomId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListPublic_RoomsWithCounts_MapsToSummaries()
    {
        var room = new Room("Jazz", 1, false, InviteCode.Generate(), TestClock.Now).WithId(3);
        _rooms.ListPublicAsync(Arg.Any<CancellationToken>()).Returns([(room, 12)]);

        var result = await _service.ListPublicAsync(default);

        result.ShouldHaveSingleItem().ShouldBe(new RoomSummaryDto(3, "Jazz", 12));
    }

    private Room GivenRoom(bool isPrivate)
    {
        var room = new Room("Room", 1, isPrivate, InviteCode.Generate(), TestClock.Now.AddDays(-1)).WithId(RoomId);
        _rooms.GetByIdAsync(RoomId, Arg.Any<CancellationToken>()).Returns(room);
        return room;
    }

    private RoomMember GivenMember(long userId, RoomRole role)
    {
        var member = new RoomMember(RoomId, userId, role, TestClock.Now.AddHours(-2));
        _rooms.GetMemberAsync(RoomId, userId, Arg.Any<CancellationToken>()).Returns(member);
        return member;
    }
}
