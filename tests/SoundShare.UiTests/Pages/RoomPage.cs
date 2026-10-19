namespace SoundShare.UiTests.Pages;

public sealed class RoomPage(IPage page)
{
    public ILocator Title => page.GetByTestId("room-title");

    public ILocator InviteCode => page.GetByTestId("room-code");

    public ILocator MyRole => page.GetByTestId("my-role");

    public ILocator QueueItems => page.GetByTestId("queue-item");

    public ILocator PlayingTitle => page.GetByTestId("playing-title");

    public ILocator ActionError => page.GetByTestId("room-action-error");

    public ILocator Item(string title) => QueueItems.Filter(new LocatorFilterOptions { HasText = title });

    public ILocator Score(string title) => Item(title).GetByTestId("score");

    public async Task SearchAndAddAsync(string query)
    {
        await page.GetByTestId("search-query").FillAsync(query);
        await page.GetByTestId("search").ClickAsync();
        await page.GetByTestId("add-track").First.ClickAsync();
    }

    public async Task VoteDownAsync(string title) => await Item(title).GetByTestId("vote-down").ClickAsync();

    public async Task VoteUpAsync(string title) => await Item(title).GetByTestId("vote-up").ClickAsync();

    public async Task LeaveAsync() => await page.GetByTestId("leave-room").ClickAsync();
}
