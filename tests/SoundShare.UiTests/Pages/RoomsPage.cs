namespace SoundShare.UiTests.Pages;

public sealed class RoomsPage(IPage page)
{
    public ILocator Heading => page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Комнаты", Exact = true });

    public ILocator CurrentUser => page.GetByTestId("current-user");

    public ILocator Error => page.GetByTestId("rooms-error");

    public async Task<RoomPage> CreateRoomAsync(string name, bool isPrivate = false)
    {
        await page.GetByTestId("room-name").FillAsync(name);
        if (isPrivate)
        {
            await page.GetByTestId("room-private").CheckAsync();
        }

        await page.GetByTestId("create-room").ClickAsync();
        return new RoomPage(page);
    }

    public async Task<RoomPage> JoinByCodeAsync(string code)
    {
        await page.GetByTestId("invite-code").FillAsync(code);
        await page.GetByTestId("join-by-code").ClickAsync();
        return new RoomPage(page);
    }

    public async Task LogoutAsync() => await page.GetByTestId("logout").ClickAsync();
}
