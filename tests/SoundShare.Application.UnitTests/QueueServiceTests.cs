using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.Application.Validation;
using SoundShare.Domain.Catalog;
using SoundShare.Domain.Queue;
using SoundShare.Domain.Rooms;

namespace SoundShare.Application.UnitTests;

public class QueueServiceTests
{
    private const long RoomId = 10;
    private const long UserId = 7;

    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly IQueueRepository _queues = Substitute.For<IQueueRepository>();
    private readonly ICatalogRepository _catalog = Substitute.For<ICatalogRepository>();
    private readonly IRoomNotifier _notifier = Substitute.For<IRoomNotifier>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Playlist _queue = Playlist.ForRoom(RoomId, "Room", TestClock.Now).WithId(100);
    private readonly QueueService _service;

    public QueueServiceTests()
    {
        var time = TestClock.Fixed();
        var rooms = new RoomService(_rooms, _queues, _unitOfWork, time, _notifier,
            new CreateRoomRequestValidator(), new JoinByCodeRequestValidator(), new BanRequestValidator());
        _service = new QueueService(_queues, _catalog, rooms, _unitOfWork, time, _notifier, new VoteRequestValidator());
        _rooms.GetByIdAsync(RoomId, Arg.Any<CancellationToken>()).Returns(new Room("Room", 1, false, InviteCode.Generate(), TestClock.Now).WithId(RoomId));
        _queues.GetRoomQueueAsync(RoomId, Arg.Any<CancellationToken>()).Returns(_queue);
    }

    [Fact]
    public async Task AddItem_NonMember_ThrowsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => _service.AddItemAsync(RoomId, UserId, new AddQueueItemRequest(5), default));

        _queues.DidNotReceive().AddItem(Arg.Any<QueueItem>());
    }

    [Fact]
    public async Task AddItem_UnknownTrack_ThrowsNotFound()
    {
        AsMember(RoomRole.Contributor);

        await Should.ThrowAsync<NotFoundException>(() => _service.AddItemAsync(RoomId, UserId, new AddQueueItemRequest(5), default));
    }

    [Fact]
    public async Task AddItem_UserAlreadyHasThreeTracks_ThrowsConflict()
    {
        AsMember(RoomRole.Contributor);
        _catalog.GetTrackAsync(5, Arg.Any<CancellationToken>()).Returns(NewTrack(5));
        _queues.CountActiveItemsByUserAsync(_queue.Id, UserId, Arg.Any<CancellationToken>()).Returns(QueueRules.MaxActiveItemsPerUser);

        await Should.ThrowAsync<ConflictException>(() => _service.AddItemAsync(RoomId, UserId, new AddQueueItemRequest(5), default));

        _queues.DidNotReceive().AddItem(Arg.Any<QueueItem>());
        await _notifier.DidNotReceive().QueueChangedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItem_ThirdTrack_AddsItemWithAuthorUpvoteAndNotifies()
    {
        AsMember(RoomRole.Contributor);
        var track = NewTrack(5);
        _catalog.GetTrackAsync(5, Arg.Any<CancellationToken>()).Returns(track);
        _queues.CountActiveItemsByUserAsync(_queue.Id, UserId, Arg.Any<CancellationToken>()).Returns(2);
        var current = Playing(50);
        _queues.GetPlayingAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(current);
        QueueItem? added = null;
        _queues.When(q => q.AddItem(Arg.Any<QueueItem>())).Do(call => added = call.Arg<QueueItem>().With("Track", track));
        _queues.GetActiveItemsAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(_ => new List<QueueItemView> { new(added!, 1, "user_7") });

        var result = await _service.AddItemAsync(RoomId, UserId, new AddQueueItemRequest(5), default);

        result.Score.ShouldBe(1);
        result.Track.Id.ShouldBe(5);
        added!.Status.ShouldBe(QueueItemStatus.Queued);
        _queues.Received(1).AddVote(Arg.Is<TrackVote>(v => v.UserId == UserId && v.VoteValue == 1));
        await _notifier.Received(1).QueueChangedAsync(RoomId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItem_NothingPlaying_StartsAddedTrack()
    {
        AsMember(RoomRole.Contributor);
        var track = NewTrack(5);
        _catalog.GetTrackAsync(5, Arg.Any<CancellationToken>()).Returns(track);
        QueueItem? added = null;
        _queues.When(q => q.AddItem(Arg.Any<QueueItem>())).Do(call => added = call.Arg<QueueItem>().With("Track", track));
        _queues.GetActiveItemsAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(_ => new List<QueueItemView> { new(added!, 1, "user_7") });

        await _service.AddItemAsync(RoomId, UserId, new AddQueueItemRequest(5), default);

        added!.Status.ShouldBe(QueueItemStatus.Playing);
        added.StartedAt.ShouldBe(TestClock.Now);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-2)]
    [InlineData(100)]
    public async Task Vote_InvalidValue_ThrowsValidation(int value)
    {
        AsMember(RoomRole.Contributor);

        await Should.ThrowAsync<ValidationException>(() => _service.VoteAsync(RoomId, UserId, 1, new VoteRequest(value), default));

        _queues.DidNotReceive().AddVote(Arg.Any<TrackVote>());
    }

    [Fact]
    public async Task Vote_ScoreReachesMinusFive_AutoRemovesItem()
    {
        AsMember(RoomRole.Contributor);
        var item = Queued(1);
        _queues.GetScoreAsync(1, Arg.Any<CancellationToken>()).Returns(QueueRules.AutoRemoveThreshold);

        var result = await _service.VoteAsync(RoomId, UserId, 1, new VoteRequest(-1), default);

        result.Score.ShouldBe(-5);
        result.Status.ShouldBe("AutoRemoved");
        item.Status.ShouldBe(QueueItemStatus.AutoRemoved);
        await _queues.Received(1).LockItemAsync(1, Arg.Any<CancellationToken>());
        await _notifier.Received(1).QueueChangedAsync(RoomId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Vote_ScoreAboveThreshold_KeepsItemQueued(int score)
    {
        AsMember(RoomRole.Contributor);
        var item = Queued(1);
        _queues.GetScoreAsync(1, Arg.Any<CancellationToken>()).Returns(score);

        var result = await _service.VoteAsync(RoomId, UserId, 1, new VoteRequest(-1), default);

        result.Status.ShouldBe("Queued");
        item.Status.ShouldBe(QueueItemStatus.Queued);
    }

    [Fact]
    public async Task Vote_PinnedItemBelowThreshold_IsNotRemoved()
    {
        AsMember(RoomRole.Contributor);
        var item = Queued(1);
        item.Pin(TestClock.Now);
        _queues.GetScoreAsync(1, Arg.Any<CancellationToken>()).Returns(-7);

        var result = await _service.VoteAsync(RoomId, UserId, 1, new VoteRequest(-1), default);

        result.Status.ShouldBe("Queued");
    }

    [Fact]
    public async Task Vote_FirstVote_AddsVote()
    {
        AsMember(RoomRole.Contributor);
        Queued(1);

        await _service.VoteAsync(RoomId, UserId, 1, new VoteRequest(1), default);

        _queues.Received(1).AddVote(Arg.Is<TrackVote>(v => v.PlaylistTrackId == 1 && v.UserId == UserId && v.VoteValue == 1));
    }

    [Fact]
    public async Task Vote_ExistingVoteChanged_UpdatesVoteValue()
    {
        AsMember(RoomRole.Contributor);
        Queued(1);
        var vote = new TrackVote(1, UserId, 1, TestClock.Now.AddMinutes(-5));
        _queues.GetVoteAsync(1, UserId, Arg.Any<CancellationToken>()).Returns(vote);

        await _service.VoteAsync(RoomId, UserId, 1, new VoteRequest(-1), default);

        vote.VoteValue.ShouldBe(-1);
        _queues.DidNotReceive().AddVote(Arg.Any<TrackVote>());
    }

    [Fact]
    public async Task Vote_ZeroWithExistingVote_RemovesVote()
    {
        AsMember(RoomRole.Contributor);
        Queued(1);
        var vote = new TrackVote(1, UserId, 1, TestClock.Now.AddMinutes(-5));
        _queues.GetVoteAsync(1, UserId, Arg.Any<CancellationToken>()).Returns(vote);

        await _service.VoteAsync(RoomId, UserId, 1, new VoteRequest(0), default);

        _queues.Received(1).RemoveVote(vote);
    }

    [Fact]
    public async Task Vote_ItemFromAnotherRoom_ThrowsNotFound()
    {
        AsMember(RoomRole.Contributor);
        var foreign = new QueueItem(999, 1, 99, TestClock.Now).WithId(1);
        _queues.GetItemAsync(1, Arg.Any<CancellationToken>()).Returns(foreign);

        await Should.ThrowAsync<NotFoundException>(() => _service.VoteAsync(RoomId, UserId, 1, new VoteRequest(1), default));
    }

    [Fact]
    public async Task Pin_Contributor_ThrowsForbiddenAndSavesNothing()
    {
        AsMember(RoomRole.Contributor);
        var item = Queued(1);

        await Should.ThrowAsync<ForbiddenException>(() => _service.SetPinnedAsync(RoomId, UserId, 1, true, default));

        item.IsPinned.ShouldBeFalse();
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(RoomRole.Moderator)]
    [InlineData(RoomRole.Owner)]
    public async Task Pin_ModeratorOrOwner_PinsItemAndBumpsVersion(RoomRole role)
    {
        AsMember(role);
        var item = Queued(1);
        var version = _queue.QueueVersion;

        await _service.SetPinnedAsync(RoomId, UserId, 1, true, default);

        item.IsPinned.ShouldBeTrue();
        _queue.QueueVersion.ShouldBe(version + 1);
        await _notifier.Received(1).QueueChangedAsync(RoomId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unpin_Moderator_UnpinsItem()
    {
        AsMember(RoomRole.Moderator);
        var item = Queued(1);
        item.Pin(TestClock.Now);

        await _service.SetPinnedAsync(RoomId, UserId, 1, false, default);

        item.IsPinned.ShouldBeFalse();
    }

    [Fact]
    public async Task Remove_OtherUsersTrackByContributor_ThrowsForbidden()
    {
        AsMember(RoomRole.Contributor);
        var item = Queued(1, addedBy: 99);

        await Should.ThrowAsync<ForbiddenException>(() => _service.RemoveItemAsync(RoomId, UserId, 1, default));

        item.Status.ShouldBe(QueueItemStatus.Queued);
    }

    [Fact]
    public async Task Remove_OwnTrack_MarksRemoved()
    {
        AsMember(RoomRole.Contributor);
        var item = Queued(1, addedBy: UserId);

        await _service.RemoveItemAsync(RoomId, UserId, 1, default);

        item.Status.ShouldBe(QueueItemStatus.Removed);
    }

    [Fact]
    public async Task Remove_OtherUsersTrackByModerator_MarksRemoved()
    {
        AsMember(RoomRole.Moderator);
        var item = Queued(1, addedBy: 99);

        await _service.RemoveItemAsync(RoomId, UserId, 1, default);

        item.Status.ShouldBe(QueueItemStatus.Removed);
    }

    [Fact]
    public async Task VoteSkip_MajorityReached_SkipsAndStartsNextTrack()
    {
        AsMember(RoomRole.Contributor);
        var playing = Playing(1);
        var next = Queued(2);
        _queues.GetPlayingAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(playing);
        _queues.CountSkipVotesAsync(1, Arg.Any<CancellationToken>()).Returns(2);
        _rooms.CountActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns(3);
        _queues.GetActiveItemsAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns([new QueueItemView(next, 0, "user_99")]);

        var progress = await _service.VoteSkipAsync(RoomId, UserId, default);

        progress.ShouldBe(new SkipProgressDto(2, 2, true));
        playing.Status.ShouldBe(QueueItemStatus.Skipped);
        next.Status.ShouldBe(QueueItemStatus.Playing);
        _queues.Received(1).AddSkipVote(Arg.Is<SkipVote>(v => v.PlaylistTrackId == 1 && v.UserId == UserId));
        await _notifier.Received(1).PlaybackChangedAsync(RoomId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoteSkip_HalfOfMembers_DoesNotSkip()
    {
        AsMember(RoomRole.Contributor);
        var playing = Playing(1);
        _queues.GetPlayingAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(playing);
        _queues.CountSkipVotesAsync(1, Arg.Any<CancellationToken>()).Returns(2);
        _rooms.CountActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns(4);

        var progress = await _service.VoteSkipAsync(RoomId, UserId, default);

        progress.ShouldBe(new SkipProgressDto(2, 3, false));
        playing.Status.ShouldBe(QueueItemStatus.Playing);
        await _notifier.DidNotReceive().PlaybackChangedAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoteSkip_RepeatedVote_IsNotCountedTwice()
    {
        AsMember(RoomRole.Contributor);
        var playing = Playing(1);
        _queues.GetPlayingAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(playing);
        _queues.HasSkipVoteAsync(1, UserId, Arg.Any<CancellationToken>()).Returns(true);
        _queues.CountSkipVotesAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _rooms.CountActiveMembersAsync(RoomId, Arg.Any<CancellationToken>()).Returns(5);

        await _service.VoteSkipAsync(RoomId, UserId, default);

        _queues.DidNotReceive().AddSkipVote(Arg.Any<SkipVote>());
    }

    [Fact]
    public async Task VoteSkip_NothingPlaying_ThrowsConflict()
    {
        AsMember(RoomRole.Contributor);

        await Should.ThrowAsync<ConflictException>(() => _service.VoteSkipAsync(RoomId, UserId, default));
    }

    [Fact]
    public async Task Skip_Contributor_ThrowsForbidden()
    {
        AsMember(RoomRole.Contributor);

        await Should.ThrowAsync<ForbiddenException>(() => _service.SkipAsync(RoomId, UserId, default));
    }

    [Fact]
    public async Task Skip_Moderator_StartsPinnedTrackFirst()
    {
        AsMember(RoomRole.Moderator);
        var playing = Playing(1);
        var popular = Queued(2);
        var pinned = Queued(3);
        pinned.Pin(TestClock.Now);
        _queues.GetPlayingAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(playing);
        _queues.GetActiveItemsAsync(_queue.Id, Arg.Any<CancellationToken>())
            .Returns([new QueueItemView(popular, 10, "a"), new QueueItemView(pinned, -2, "b")]);

        await _service.SkipAsync(RoomId, UserId, default);

        playing.Status.ShouldBe(QueueItemStatus.Skipped);
        pinned.Status.ShouldBe(QueueItemStatus.Playing);
        popular.Status.ShouldBe(QueueItemStatus.Queued);
    }

    [Fact]
    public async Task GetQueue_MixedItems_OrdersPinnedThenScoreThenAddedTime()
    {
        AsMember(RoomRole.Contributor);
        var playing = Playing(1);
        var high = Queued(2);
        var low = Queued(3);
        var pinned = Queued(4);
        pinned.Pin(TestClock.Now);
        _queues.GetActiveItemsAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(
        [
            new QueueItemView(playing, 0, "a"),
            new QueueItemView(low, 1, "b"),
            new QueueItemView(high, 5, "c"),
            new QueueItemView(pinned, -3, "d"),
        ]);

        var queue = await _service.GetQueueAsync(RoomId, UserId, default);

        queue.Playing!.ItemId.ShouldBe(1);
        queue.Items.Select(i => i.Id).ShouldBe([4L, 2L, 3L]);
        queue.Items[0].Pinned.ShouldBeTrue();
    }

    [Fact]
    public async Task AdvanceFinishedTracks_TrackEnded_MarksPlayedAndStartsNext()
    {
        var playing = new QueueItem(_queue.Id, 1, 99, TestClock.Now.AddMinutes(-10)).WithId(1).With("Track", NewTrack(1));
        playing.StartPlaying(TestClock.Now.AddSeconds(-250));
        var next = Queued(2);
        _queues.GetRoomIdsWithFinishedTrackAsync(TestClock.Now, Arg.Any<CancellationToken>()).Returns([RoomId]);
        _queues.GetPlayingAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns(playing);
        _queues.GetActiveItemsAsync(_queue.Id, Arg.Any<CancellationToken>()).Returns([new QueueItemView(next, 0, "a")]);

        var advanced = await _service.AdvanceFinishedTracksAsync(default);

        advanced.ShouldBe(1);
        playing.Status.ShouldBe(QueueItemStatus.Played);
        next.Status.ShouldBe(QueueItemStatus.Playing);
    }

    private void AsMember(RoomRole role) =>
        _rooms.GetMemberAsync(RoomId, UserId, Arg.Any<CancellationToken>()).Returns(new RoomMember(RoomId, UserId, role, TestClock.Now.AddHours(-1)));

    private static Track NewTrack(long id) =>
        new Track($"Track {id}", 200, $"vid{id:D8}", TestClock.Now).WithId(id);

    private QueueItem Queued(long id, long addedBy = 99)
    {
        var item = new QueueItem(_queue.Id, id, addedBy, TestClock.Now.AddMinutes(-id)).WithId(id).With("Track", NewTrack(id));
        _queues.GetItemAsync(id, Arg.Any<CancellationToken>()).Returns(item);
        return item;
    }

    private QueueItem Playing(long id)
    {
        var item = Queued(id);
        item.StartPlaying(TestClock.Now.AddSeconds(-30));
        return item;
    }
}
