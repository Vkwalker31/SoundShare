// Prepares load test data through the public API: users, one room they all join, and queue items to vote on.
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SoundShare.LoadTests;

internal sealed record LoadUser(string Username, string Token);

internal sealed record LoadRoom(long RoomId, IReadOnlyList<LoadUser> Members, IReadOnlyList<long> ItemIds);

internal static class LoadData
{
    private const string Password = "Load_Test_Pass1";

    public static async Task<LoadRoom> PrepareAsync(HttpClient http, int count)
    {
        var run = DateTime.UtcNow.ToString("HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var members = new List<LoadUser>();
        for (var i = 0; i < count; i++)
        {
            var username = $"load_{run}_{i}";
            (await http.PostAsJsonAsync("api/auth/register", new { username, email = $"{username}@example.com", password = Password })).EnsureSuccessStatusCode();
            var login = await http.PostAsJsonAsync("api/auth/login", new { username, password = Password });
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
            members.Add(new LoadUser(username, token));
        }

        var room = await Send<RoomCreated>(http, HttpMethod.Post, "api/rooms", members[0].Token, new { name = $"Load {run}", isPrivate = false });
        foreach (var member in members.Skip(1))
        {
            await Send<object>(http, HttpMethod.Post, $"api/rooms/{room.Id}/members", member.Token, null);
        }

        var tracks = await Send<SearchResult>(http, HttpMethod.Get, "api/tracks?query=e", members[0].Token, null);
        var items = new List<long>();
        for (var i = 0; i < Math.Min(tracks.Items.Count, members.Count); i++)
        {
            var item = await Send<QueueItem>(http, HttpMethod.Post, $"api/rooms/{room.Id}/queue/items", members[i].Token, new { trackId = tracks.Items[i].Id });
            items.Add(item.Id);
        }

        return new LoadRoom(room.Id, members, items);
    }

    private static async Task<T> Send<T>(HttpClient http, HttpMethod method, string url, string token, object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed record TokenResponse(string AccessToken);

    private sealed record RoomCreated(long Id);

    private sealed record SearchResult(IReadOnlyList<Track> Items);

    private sealed record Track(long Id);

    private sealed record QueueItem(long Id);
}
