using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SoundShare.Application.Contracts;
using SoundShare.IntegrationTests.Infrastructure;

namespace SoundShare.IntegrationTests.Scenarios;

[Collection(SoundShareTestGroup.Name)]
public class SecurityScenarioTests(SoundShareFixture fixture)
{
    [Theory]
    [InlineData("/api/rooms")]
    [InlineData("/api/users/me")]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/audit-logs")]
    [InlineData("/api/tracks?query=rock")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401(string url)
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_TamperedToken_Returns401()
    {
        var user = await fixture.RegisterAsync();
        var token = user.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        (await client.GetAsync("/api/users/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/audit-logs")]
    public async Task AdminEndpoint_RegularUser_Returns403(string url)
    {
        var user = await fixture.RegisterAsync();

        (await user.Client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PrivateRoom_NonMemberGuessesId_Returns404()
    {
        var owner = await fixture.RegisterAsync();
        var stranger = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync(isPrivate: true);

        (await stranger.Client.GetAsync($"/api/rooms/{room.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.Client.GetAsync($"/api/rooms/{room.Id}/queue")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.Client.PostAsync($"/api/rooms/{room.Id}/members", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ForeignQueueItem_VoteThroughAnotherRoom_Returns404()
    {
        var owner = await fixture.RegisterAsync();
        var other = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync();
        var item = await owner.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(owner));
        var otherRoom = await other.CreateRoomAsync();

        var response = await other.Client.PutAsJsonAsync($"/api/rooms/{otherRoom.Id}/queue/items/{item.Id}/vote", new VoteRequest(-1));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FourthTrackFromSameUser_Returns409()
    {
        var owner = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync();
        for (var i = 0; i < 3; i++)
        {
            await owner.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(owner));
        }

        var response = await owner.Client.PostAsJsonAsync($"/api/rooms/{room.Id}/queue/items", new AddQueueItemRequest(await fixture.ImportTrackAsync(owner)));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvalidRequest_ReturnsProblemDetailsWithoutInternals()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("x", "not-an-email", "1"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        body.ShouldContain("errors");
        body.ShouldNotContain("   at ");
        body.ShouldNotContain("Exception");
        body.ShouldNotContain("ORA-");
        body.ShouldNotContain("SOUNDSHARE");
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        var user = await fixture.RegisterAsync();

        (await user.Client.GetAsync("/api/does-not-exist")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicRoomsList_ShowsMemberCount()
    {
        var owner = await fixture.RegisterAsync();
        var guest = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync();
        await guest.JoinAsync(room.Id);

        var rooms = await guest.Client.GetFromJsonAsync<List<RoomSummaryDto>>("/api/rooms");

        rooms!.ShouldContain(r => r.Id == room.Id && r.MembersCount == 2);
    }

    [Fact]
    public async Task FailedRequest_IsAuditedWithRealStatusCode()
    {
        var admin = await fixture.RegisterAdminAsync();
        var owner = await fixture.RegisterAsync();
        var contributor = await fixture.RegisterAsync();
        var room = await owner.CreateRoomAsync();
        await contributor.JoinAsync(room.Id);
        var item = await owner.AddTrackAsync(room.Id, await fixture.ImportTrackAsync(owner));

        (await contributor.Client.PutAsync($"/api/rooms/{room.Id}/queue/items/{item.Id}/pin", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        AuditLogDto? entry = null;
        for (var attempt = 0; attempt < 30 && entry is null; attempt++)
        {
            await Task.Delay(500);
            var page = await admin.Client.GetFromJsonAsync<AuditPageDto>($"/api/admin/audit-logs?userId={contributor.Id}&action=Queue.PinFailed");
            entry = page!.Items.Count > 0 ? page.Items[0] : null;
        }

        entry.ShouldNotBeNull();
        entry.StatusCode.ShouldBe(403);
        entry.HttpMethod.ShouldBe("PUT");
        entry.CorrelationId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Response_ContainsCorrelationIdHeader()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReadinessCheck_WithDatabase_ReturnsHealthy()
    {
        using var client = fixture.CreateClient();

        (await client.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
