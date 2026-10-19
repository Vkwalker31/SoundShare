// HTTP helpers for scenario tests: users with their own clients, rooms, tracks imported through the YouTube stub.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using SoundShare.Application.Contracts;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace SoundShare.IntegrationTests.Infrastructure;

public sealed record TestUser(long Id, string Username, HttpClient Client);

public sealed record CreatedUser(long Id, string Username);

public sealed record TokenResponse(string AccessToken, int ExpiresIn);

public static class TestApi
{
    public const string Password = "Secret_123";

    private const string VideoIdAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";

    public static HttpClient CreateClient(this SoundShareFixture fixture, bool handleCookies = true) =>
        fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
            AllowAutoRedirect = false,
        });

    public static string UniqueName(string prefix) => (prefix + Guid.NewGuid().ToString("N"))[..16];

    public static string NewVideoId() => RandomNumberGenerator.GetString(VideoIdAlphabet, 11);

    public static async Task<TestUser> RegisterAsync(this SoundShareFixture fixture, string prefix = "u")
    {
        var username = UniqueName(prefix);
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, $"{username}@example.com", Password));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedUser>();
        await client.LoginAsync(username);
        return new TestUser(created!.Id, username, client);
    }

    public static async Task<TestUser> RegisterAdminAsync(this SoundShareFixture fixture)
    {
        var user = await fixture.RegisterAsync("adm");
        await fixture.ExecuteOwnerSqlAsync("UPDATE users SET role_id = 1 WHERE id = :id", ("id", user.Id));
        await user.Client.LoginAsync(user.Username);
        return user;
    }

    public static async Task LoginAsync(this HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, Password));
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }

    public static async Task<RoomCreatedDto> CreateRoomAsync(this TestUser owner, bool isPrivate = false)
    {
        var response = await owner.Client.PostAsJsonAsync("/api/rooms", new CreateRoomRequest($"Room {owner.Username}", isPrivate));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomCreatedDto>())!;
    }

    public static async Task JoinAsync(this TestUser user, long roomId)
    {
        var response = await user.Client.PostAsync($"/api/rooms/{roomId}/members", null);
        response.EnsureSuccessStatusCode();
    }

    public static void StubYouTube(this SoundShareFixture fixture, string query, params (string VideoId, string Title, string Channel, string Duration)[] videos)
    {
        fixture.YouTube
            .Given(Request.Create().WithPath("/youtube/v3/search").WithParam("q", query).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                items = videos.Select(v => new { id = new { kind = "youtube#video", videoId = v.VideoId } }).ToArray(),
            }));
        fixture.YouTube
            .Given(Request.Create().WithPath("/youtube/v3/videos").WithParam("id", string.Join(',', videos.Select(v => v.VideoId))).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                items = videos.Select(v => new
                {
                    id = v.VideoId,
                    snippet = new { title = v.Title, channelTitle = v.Channel },
                    contentDetails = new { duration = v.Duration },
                }).ToArray(),
            }));
    }

    public static async Task<long> ImportTrackAsync(this SoundShareFixture fixture, TestUser user, string title = "Test Artist - Test Song")
    {
        var query = UniqueName("q");
        fixture.StubYouTube(query, (NewVideoId(), title, "Test Channel", "PT3M20S"));
        var result = await user.Client.GetFromJsonAsync<TrackSearchResultDto>($"/api/tracks?query={query}");
        return result!.Items.ShouldHaveSingleItem().Id;
    }

    public static async Task<QueueItemDto> AddTrackAsync(this TestUser user, long roomId, long trackId)
    {
        var response = await user.Client.PostAsJsonAsync($"/api/rooms/{roomId}/queue/items", new AddQueueItemRequest(trackId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QueueItemDto>())!;
    }

    public static async Task<VoteResultDto> VoteAsync(this TestUser user, long roomId, long itemId, int value)
    {
        var response = await user.Client.PutAsJsonAsync($"/api/rooms/{roomId}/queue/items/{itemId}/vote", new VoteRequest(value));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VoteResultDto>())!;
    }

    public static async Task<QueueDto> GetQueueAsync(this TestUser user, long roomId) =>
        (await user.Client.GetFromJsonAsync<QueueDto>($"/api/rooms/{roomId}/queue"))!;
}
