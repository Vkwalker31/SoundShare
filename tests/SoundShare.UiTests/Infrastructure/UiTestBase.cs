// Each test gets its own browser context; on failure a full-page screenshot and a Playwright trace are saved to TestResults/ui.
using System.Runtime.CompilerServices;

namespace SoundShare.UiTests.Infrastructure;

public abstract class UiTestBase(UiFixture fixture)
{
    protected UiFixture Fixture { get; } = fixture;

    protected static string ArtifactsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "TestResults", "ui");

    protected async Task<IBrowserContext> NewContextAsync(BrowserNewContextOptions? options = null)
    {
        options ??= new BrowserNewContextOptions();
        options.BaseURL = Fixture.BaseUrl.ToString();
        options.Locale = "ru-RU";
        var context = await Fixture.Browser.NewContextAsync(options);
        context.SetDefaultTimeout(10_000);
        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });
        return context;
    }

    protected async Task RunAsync(Func<IPage, Task> scenario, BrowserNewContextOptions? options = null, [CallerMemberName] string name = "")
    {
        var context = await NewContextAsync(options);
        var page = await context.NewPageAsync();
        try
        {
            await scenario(page);
            await context.Tracing.StopAsync();
        }
        catch
        {
            Directory.CreateDirectory(ArtifactsDirectory);
            var file = Path.Combine(ArtifactsDirectory, $"{Fixture.BrowserName}-{name}");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = file + ".png", FullPage = true });
            await context.Tracing.StopAsync(new TracingStopOptions { Path = file + ".zip" });
            throw;
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
