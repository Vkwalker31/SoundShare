// Layout and compatibility sweep: every page at several widths; collects overflow, console errors, failed requests and screenshots.
using System.Text.Json;
using SoundShare.UiTests.Infrastructure;
using SoundShare.UiTests.Pages;

namespace SoundShare.UiTests;

[Collection(UiTestGroup.Name)]
public class CompatibilityTests(UiFixture fixture) : UiTestBase(fixture)
{
    private static readonly int[] Widths = [360, 768, 1280, 1920, 2560, 640];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Fact]
    public async Task AllPages_AllWidths_NoOverflowNoConsoleErrors()
    {
        var account = await Fixture.CreateAccountAsync("cmp");
        var longRoom = new string('Ш', 60) + " " + account.Username;
        var results = new List<Dictionary<string, object>>();
        var output = Path.Combine(ArtifactsDirectory, "..", "compat", Fixture.BrowserName);
        Directory.CreateDirectory(output);

        foreach (var width in Widths)
        {
            var mobile = width == 360 && Fixture.BrowserName != "firefox";
            var context = await NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = width, Height = width <= 768 ? 800 : 900 },
                IsMobile = mobile,
                HasTouch = mobile,
            });
            await context.AddInitScriptAsync("localStorage.setItem('soundshare.video', 'off')");
            var page = await context.NewPageAsync();
            var consoleErrors = new List<string>();
            var failed = new List<string>();
            page.Console += (_, message) =>
            {
                if (message.Type == "error")
                {
                    consoleErrors.Add(message.Text);
                }
            };
            page.Response += (_, response) =>
            {
                if (response.Status >= 400)
                {
                    failed.Add($"{response.Status} {new Uri(response.Url).AbsolutePath}");
                }
            };

            async Task Check(string name)
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                var overflow = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(output, $"{name}-{width}.png"), FullPage = true });
                results.Add(new Dictionary<string, object>
                {
                    ["page"] = name,
                    ["width"] = width,
                    ["mobile"] = mobile,
                    ["overflowPx"] = overflow,
                    ["consoleErrors"] = consoleErrors.ToArray(),
                    ["failedRequests"] = failed.ToArray(),
                });
                consoleErrors.Clear();
                failed.Clear();
            }

            var login = new LoginPage(page);
            await login.OpenAsync();
            await Expect(page.GetByTestId("login-form")).ToBeVisibleAsync();
            await Check("login");

            var rooms = await login.LoginAsync(account.Username, account.Password);
            await Expect(rooms.Heading).ToBeVisibleAsync();
            await Check("rooms");

            await page.GotoAsync("rooms/1");
            await Expect(page.GetByTestId("queue-item").First).ToBeVisibleAsync();
            await Check("room-full-queue");

            await page.GotoAsync("");
            var room = await rooms.CreateRoomAsync(longRoom + width.ToString(System.Globalization.CultureInfo.InvariantCulture)[..1]);
            await Expect(page.GetByTestId("queue-empty")).ToBeVisibleAsync();
            await Check("room-empty-long-name");

            await page.GotoAsync("not-existing-page");
            await Expect(page.GetByTestId("not-found")).ToBeVisibleAsync();
            await Check("not-found");

            await rooms.LogoutAsync();
            await login.LoginAsync("admin", "Demo_Pass1");
            await Expect(page.GetByTestId("current-user")).ToHaveTextAsync("admin");
            await page.GotoAsync("admin/users");
            await Expect(page.GetByTestId("users-table")).ToBeVisibleAsync();
            await Check("admin-users");
            await page.GotoAsync("admin/audit");
            await Expect(page.GetByTestId("audit-table")).ToBeVisibleAsync();
            await Check("admin-audit");

            await context.Tracing.StopAsync();
            await context.CloseAsync();
        }

        var interactive = await CheckInteractiveAsync(account);
        await File.WriteAllTextAsync(
            Path.Combine(output, "results.json"),
            JsonSerializer.Serialize(new { browser = Fixture.BrowserName, pages = results, interactive }, JsonOptions));

        results.Where(r => (int)r["overflowPx"] > 0).ShouldBeEmpty();
        results.Where(r => ((string[])r["consoleErrors"]).Length > 0).ShouldBeEmpty();
        results.Where(r => ((string[])r["failedRequests"]).Length > 0).ShouldBeEmpty();
    }

    private async Task<Dictionary<string, object>> CheckInteractiveAsync(TestAccount account)
    {
        var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var login = new LoginPage(page);
        await login.OpenAsync();
        await login.LoginAsync(account.Username, account.Password);
        await Expect(page.GetByTestId("current-user")).ToBeVisibleAsync();

        var favicon = await page.EvaluateAsync<string>("() => document.querySelector('link[rel=icon]')?.getAttribute('href') ?? ''");
        var faviconStatus = (await page.APIRequest.GetAsync(favicon)).Status;
        await page.GotoAsync("rooms/1");
        await Expect(page.GetByTestId("room-title")).ToBeVisibleAsync();
        await page.GetByTestId("logo").ClickAsync();
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/$"));
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        var outline = await page.EvaluateAsync<string>("() => getComputedStyle(document.activeElement).outlineStyle");
        var hoverBefore = await page.GetByTestId("open-room").First.EvaluateAsync<string>("e => getComputedStyle(e).borderColor");
        await page.GetByTestId("open-room").First.HoverAsync();
        var hoverAfter = await page.GetByTestId("open-room").First.EvaluateAsync<string>("e => getComputedStyle(e).borderColor");
        var title = await page.TitleAsync();
        await context.Tracing.StopAsync();
        await context.CloseAsync();

        return new Dictionary<string, object>
        {
            ["faviconStatus"] = faviconStatus,
            ["logoLeadsHome"] = true,
            ["focusOutline"] = outline,
            ["hoverChangesBorder"] = hoverBefore != hoverAfter,
            ["pageTitle"] = title,
        };
    }
}
