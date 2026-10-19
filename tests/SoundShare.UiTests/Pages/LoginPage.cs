namespace SoundShare.UiTests.Pages;

public sealed class LoginPage(IPage page)
{
    public ILocator Error => page.GetByTestId("login-error");

    public async Task OpenAsync() => await page.GotoAsync("login");

    public async Task<RoomsPage> LoginAsync(string username, string password)
    {
        await page.GetByTestId("login-username").FillAsync(username);
        await page.GetByTestId("login-password").FillAsync(password);
        await page.GetByTestId("login-submit").ClickAsync();
        return new RoomsPage(page);
    }

    public async Task<RoomsPage> RegisterAsync(string username, string password)
    {
        await page.GetByTestId("to-register").ClickAsync();
        await page.GetByTestId("register-username").FillAsync(username);
        await page.GetByTestId("register-email").FillAsync($"{username}@example.com");
        await page.GetByTestId("register-password").FillAsync(password);
        await page.GetByTestId("register-submit").ClickAsync();
        return new RoomsPage(page);
    }
}
