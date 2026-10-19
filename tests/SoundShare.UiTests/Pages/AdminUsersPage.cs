namespace SoundShare.UiTests.Pages;

public sealed class AdminUsersPage(IPage page)
{
    public ILocator Forbidden => page.GetByTestId("forbidden");

    public ILocator Row(string username) => page.Locator($"[data-testid=user-row][data-username='{username}']");

    public async Task OpenAsync() => await page.GotoAsync("admin/users");

    public async Task FilterBlockedAsync()
    {
        await page.GetByTestId("filter-status").SelectOptionAsync("false");
        await page.GetByTestId("apply-filters").ClickAsync();
    }
}
