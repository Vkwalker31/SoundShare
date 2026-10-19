// Browser and API access shared by UI tests. Target: SOUNDSHARE_UI_URL (default http://127.0.0.1:5290), browser: UI_BROWSER (chrome, msedge, firefox).
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace SoundShare.UiTests.Infrastructure;

public sealed record TestAccount(string Username, string Password);

public sealed class UiFixture : IAsyncLifetime
{
    public const string Password = "Ui_Test_Pass1";

    public IPlaywright Playwright { get; private set; } = null!;

    public IBrowser Browser { get; private set; } = null!;

    public string BrowserName { get; } = Environment.GetEnvironmentVariable("UI_BROWSER") ?? "chrome";

    public Uri BaseUrl { get; } = new(Environment.GetEnvironmentVariable("SOUNDSHARE_UI_URL") ?? "http://127.0.0.1:5290");

    public HttpClient Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Assertions.SetDefaultExpectTimeout(15_000);
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = BrowserName == "firefox"
            ? await Playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })
            : await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = BrowserName, Headless = true });
        Api = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = BaseUrl };
    }

    public async Task DisposeAsync()
    {
        Api.Dispose();
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }

    public async Task<TestAccount> CreateAccountAsync(string prefix = "ui")
    {
        var username = (prefix + "_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6))).ToLowerInvariant();
        var response = await Api.PostAsJsonAsync("/api/auth/register", new { username, email = $"{username}@example.com", password = Password });
        response.EnsureSuccessStatusCode();
        return new TestAccount(username, Password);
    }
}

[CollectionDefinition(Name)]
public sealed class UiTestGroup : ICollectionFixture<UiFixture>
{
    public const string Name = "UI";
}
