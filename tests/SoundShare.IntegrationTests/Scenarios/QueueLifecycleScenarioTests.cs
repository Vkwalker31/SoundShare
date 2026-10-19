using System.Net;
using System.Net.Http.Json;
using SoundShare.Application.Contracts;
using SoundShare.IntegrationTests.Infrastructure;

namespace SoundShare.IntegrationTests.Scenarios;

[Collection(SoundShareTestGroup.Name)]
public class QueueLifecycleScenarioTests(SoundShareFixture fixture)
{
    [Fact]
    public async Task QueuedTrack_DownvotedToMinusFive_BecomesAutoRemoved()
    {
        var owner = await fixture.RegisterAsync();
        var author = await fixture.RegisterAsync();
        var voters = new List<TestUser>();
        for (var i = 0; i < 5; i++)
        {
            voters.Add(await fixture.RegisterAsync());
        }

        var room = await owner.CreateRoomAsync();
        foreach (var member in voters.Append(author))
        {
            await member.JoinAsync(room.Id);
        }

        await owner.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(owner, "Band - Opening"));
        var item = await author.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(author, "Band - Unpopular"));
        item.Score.ShouldBe(1);

        VoteResultDto result = null!;
        foreach (var voter in voters.Prepend(owner))
        {
            result = await voter.VoteAsync(room.Id, item.Id, -1);
        }

        result.Score.ShouldBe(-5);
        result.Status.ShouldBe("AutoRemoved");
        (await owner.GetQueueAsync(room.Id)).Items.ShouldNotContain(i => i.Id == item.Id);
    }

    [Fact]
    public async Task QueuedTrack_DownvotedToMinusFour_StaysInQueue()
    {
        var owner = await fixture.RegisterAsync();
        var author = await fixture.RegisterAsync();
        var voters = new List<TestUser>();
        for (var i = 0; i < 4; i++)
        {
            voters.Add(await fixture.RegisterAsync());
        }

        var room = await owner.CreateRoomAsync();
        foreach (var member in voters.Append(author))
        {
            await member.JoinAsync(room.Id);
        }

        await owner.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(owner));
        var item = await author.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(author));
        VoteResultDto result = null!;
        foreach (var voter in voters.Prepend(owner))
        {
            result = await voter.VoteAsync(room.Id, item.Id, -1);
        }

        result.Score.ShouldBe(-4);
        result.Status.ShouldBe("Queued");
    }

    [Fact]
    public async Task PlayingTrack_SkipVotesReachMajority_SkippedAndNextStartsPlaying()
    {
        var owner = await fixture.RegisterAsync();
        var first = await fixture.RegisterAsync();
        var second = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync();
        await first.JoinAsync(room.Id);
        await second.JoinAsync(room.Id);
        var playing = await owner.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(owner));
        var next = await first.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(first));
        (await owner.GetQueueAsync(room.Id)).Playing!.ItemId.ShouldBe(playing.Id);

        var afterFirstVote = await first.Client.PostAsync($"/api/rooms/{room.Id}/playback/skip-votes", null);
        (await afterFirstVote.Content.ReadFromJsonAsync<SkipProgressDto>()).ShouldBe(new SkipProgressDto(1, 2, false));

        var afterSecondVote = await second.Client.PostAsync($"/api/rooms/{room.Id}/playback/skip-votes", null);
        (await afterSecondVote.Content.ReadFromJsonAsync<SkipProgressDto>())!.Skipped.ShouldBeTrue();

        var queue = await owner.GetQueueAsync(room.Id);
        queue.Playing!.ItemId.ShouldBe(next.Id);
        queue.Items.ShouldNotContain(i => i.Id == playing.Id);
    }

    [Fact]
    public async Task Moderator_PinsTrackAndSkips_PinnedTrackPlaysNext()
    {
        var owner = await fixture.RegisterAsync();
        var moderator = await fixture.RegisterAsync();
        var contributor = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync();
        await moderator.JoinAsync(room.Id);
        await contributor.JoinAsync(room.Id);
        await owner.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(owner));
        var popular = await moderator.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(moderator));
        await owner.VoteAsync(room.Id, popular.Id, 1);
        var underdog = await contributor.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(contributor));

        var contributorPin = await contributor.Client.PutAsync($"/api/rooms/{room.Id}/queue/items/{underdog.Id}/pin", null);
        contributorPin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var promote = await owner.Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/members/{moderator.Id}", new ChangeMemberRoleRequest("Moderator"));
        promote.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var pin = await moderator.Client.PutAsync($"/api/rooms/{room.Id}/queue/items/{underdog.Id}/pin", null);
        pin.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.GetQueueAsync(room.Id)).Items[0].Id.ShouldBe(underdog.Id);

        var skip = await moderator.Client.PostAsync($"/api/rooms/{room.Id}/playback/skip", null);
        skip.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.GetQueueAsync(room.Id)).Playing!.ItemId.ShouldBe(underdog.Id);
    }

    [Fact]
    public async Task Owner_LeavesRoom_OwnershipPassesToModerator()
    {
        var owner = await fixture.RegisterAsync();
        var moderator = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync();
        await moderator.JoinAsync(room.Id);
        await owner.Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/members/{moderator.Id}", new ChangeMemberRoleRequest("Moderator"));

        var leave = await owner.Client.DeleteAsync($"/api/rooms/{room.Id}/members/me");

        leave.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await moderator.Client.GetFromJsonAsync<RoomStateDto>($"/api/rooms/{room.Id}"))!.MyRole.ShouldBe("Owner");
    }

    [Fact]
    public async Task BannedMember_TriesToRejoin_GetsForbidden()
    {
        var owner = await fixture.RegisterAsync();
        var guest = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync();
        await guest.JoinAsync(room.Id);

        var ban = await owner.Client.PostAsJsonAsync($"/api/rooms/{room.Id}/bans", new BanRequest(guest.Id, 60));
        var rejoin = await guest.Client.PostAsync($"/api/rooms/{room.Id}/members", null);

        ban.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        rejoin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PrivateRoom_JoinByInviteCode_GrantsAccess()
    {
        var owner = await fixture.RegisterAsync();
        var guest = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync(isPrivate: true);

        var before = await guest.Client.GetAsync($"/api/rooms/{room.Id}");
        var join = await guest.Client.PostAsJsonAsync("/api/rooms/join", new JoinByCodeRequest(room.InviteCode.ToLowerInvariant()));
        var after = await guest.Client.GetFromJsonAsync<RoomStateDto>($"/api/rooms/{room.Id}");

        before.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        join.StatusCode.ShouldBe(HttpStatusCode.OK);
        after!.MyRole.ShouldBe("Contributor");
    }

    [Fact]
    public async Task Catalog_YouTubeUnavailable_ReturnsLocalResultsAndFlag()
    {
        var user = await fixture.RegisterAsync();
        var query = TestApi.UniqueName("down");

        var result = await user.Client.GetFromJsonAsync<TrackSearchResultDto>($"/api/tracks?query={query}");

        result!.Items.ShouldBeEmpty();
        result.ExternalSearchAvailable.ShouldBeFalse();
    }
}
