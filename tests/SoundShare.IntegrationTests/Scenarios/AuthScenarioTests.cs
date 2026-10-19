using System.Net;
using System.Net.Http.Json;
using SoundShare.Application.Contracts;
using SoundShare.IntegrationTests.Infrastructure;

namespace SoundShare.IntegrationTests.Scenarios;

[Collection(SoundShareTestGroup.Name)]
public class AuthScenarioTests(SoundShareFixture fixture)
{
    [Fact]
    public async Task Login_ValidCredentials_SetsHttpOnlyRefreshCookie()
    {
        var user = await fixture.RegisterAsync();
        using var client = fixture.CreateClient(handleCookies: false);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Username, TestApi.Password));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = response.Headers.GetValues("Set-Cookie").ShouldHaveSingleItem();
        cookie.ShouldStartWith("rt=");
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("path=/api/auth", Case.Insensitive);
        (await response.Content.ReadFromJsonAsync<TokenResponse>())!.ExpiresIn.ShouldBe(900);
    }

    [Fact]
    public async Task Refresh_RotatedTokenReused_RevokesWholeSession()
    {
        var user = await fixture.RegisterAsync();
        using var client = fixture.CreateClient(handleCookies: false);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Username, TestApi.Password));
        var original = RefreshCookie(login);

        var rotated = await RefreshAsync(client, original);
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var current = RefreshCookie(rotated);
        current.ShouldNotBe(original);

        (await RefreshAsync(client, original)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(client, current)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RefreshTokenNoLongerAccepted()
    {
        var user = await fixture.RegisterAsync();
        using var client = fixture.CreateClient(handleCookies: false);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Username, TestApi.Password));
        var cookie = RefreshCookie(login);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", cookie);
        (await client.SendAsync(logout)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RefreshAsync(client, cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = await fixture.RegisterAsync();
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Username, "Wrong_pass1"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_DuplicateUsernameInOtherCase_Returns409()
    {
        var user = await fixture.RegisterAsync();
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(user.Username.ToUpperInvariant(), $"{Guid.NewGuid():N}@example.com", TestApi.Password));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_BlocksUser_UserCannotLoginOrRefresh()
    {
        var admin = await fixture.RegisterAdminAsync();
        var user = await fixture.RegisterAsync();
        using var client = fixture.CreateClient(handleCookies: false);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Username, TestApi.Password));
        var cookie = RefreshCookie(login);

        var block = await admin.Client.PutAsync($"/api/admin/users/{user.Id}/block", null);

        block.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Username, TestApi.Password))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await RefreshAsync(client, cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_CreatesAndDemotesAnotherAdmin_Succeeds()
    {
        var admin = await fixture.RegisterAdminAsync();
        var created = await admin.Client.PostAsJsonAsync("/api/admin/users", new CreateUserRequest(TestApi.UniqueName("na"), $"{Guid.NewGuid():N}@example.com", TestApi.Password, "Admin"));
        var newAdmin = await created.Content.ReadFromJsonAsync<UserDto>();

        var demote = await admin.Client.PatchAsJsonAsync($"/api/admin/users/{newAdmin!.Id}/role", new ChangeRoleRequest("User"));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        demote.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.Client.GetFromJsonAsync<UserDto>($"/api/admin/users/{newAdmin.Id}"))!.Role.ShouldBe("User");
    }

    [Fact]
    public async Task DeleteOwnAccount_AnonymizesAndBlocksLogin()
    {
        var user = await fixture.RegisterAsync();

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me") { Content = JsonContent.Create(new DeleteAccountRequest(TestApi.Password)) };
        var delete = await user.Client.SendAsync(request);
        using var client = fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Username, TestApi.Password));

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string RefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }
}
