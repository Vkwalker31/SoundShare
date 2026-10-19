using SoundShare.UiTests.Infrastructure;
using SoundShare.UiTests.Pages;

namespace SoundShare.UiTests;

[Collection(UiTestGroup.Name)]
public class UiScenarioTests(UiFixture fixture) : UiTestBase(fixture)
{
    [Fact]
    public Task Anonymous_OpensRooms_RedirectedToLogin() => RunAsync(async page =>
    {
        await page.GotoAsync("");

        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login$"));
        await Expect(page.GetByTestId("login-form")).ToBeVisibleAsync();
    });

    [Fact]
    public Task Login_WrongPassword_ShowsErrorAndStaysOnLogin() => RunAsync(async page =>
    {
        var account = await Fixture.CreateAccountAsync();
        var login = new LoginPage(page);
        await login.OpenAsync();

        await login.LoginAsync(account.Username, "Wrong_Pass9");

        await Expect(login.Error).ToHaveTextAsync("Неверное имя пользователя или пароль.");
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login$"));
    });

    [Fact]
    public Task Register_NewAccount_LandsOnRoomsAsThatUser() => RunAsync(async page =>
    {
        var username = "reg_" + Guid.NewGuid().ToString("N")[..8];
        var login = new LoginPage(page);
        await login.OpenAsync();

        var rooms = await login.RegisterAsync(username, UiFixture.Password);

        await Expect(rooms.Heading).ToBeVisibleAsync();
        await Expect(rooms.CurrentUser).ToHaveTextAsync(username);
    });

    [Fact]
    public Task CreateRoom_AddTrack_TrackAppearsAndPlays() => RunAsync(async page =>
    {
        var account = await Fixture.CreateAccountAsync();
        var login = new LoginPage(page);
        await login.OpenAsync();
        var rooms = await login.LoginAsync(account.Username, account.Password);

        var room = await rooms.CreateRoomAsync("UI room " + account.Username);
        await Expect(room.MyRole).ToHaveTextAsync("владелец");
        await room.SearchAndAddAsync("Numb");

        await Expect(room.PlayingTitle).ToHaveTextAsync("Numb");
    });

    [Fact]
    public Task Queue_SecondTrack_ShowsAuthorUpvoteAndDownvoteChangesScore() => RunAsync(async page =>
    {
        var account = await Fixture.CreateAccountAsync();
        var login = new LoginPage(page);
        await login.OpenAsync();
        var room = await (await login.LoginAsync(account.Username, account.Password)).CreateRoomAsync("Votes " + account.Username);
        await room.SearchAndAddAsync("Numb");
        await Expect(room.PlayingTitle).ToHaveTextAsync("Numb");

        await room.SearchAndAddAsync("Thunderstruck");
        await Expect(room.Score("Thunderstruck")).ToHaveTextAsync("1");
        await room.VoteDownAsync("Thunderstruck");

        await Expect(room.Score("Thunderstruck")).ToHaveTextAsync("-1");
    });

    [Fact]
    public Task FourthTrack_ShowsLimitError() => RunAsync(async page =>
    {
        var account = await Fixture.CreateAccountAsync();
        var login = new LoginPage(page);
        await login.OpenAsync();
        var room = await (await login.LoginAsync(account.Username, account.Password)).CreateRoomAsync("Limit " + account.Username);
        foreach (var query in new[] { "Numb", "Thunderstruck", "Despacito" })
        {
            await room.SearchAndAddAsync(query);
        }

        await Expect(room.QueueItems).ToHaveCountAsync(2);
        await room.SearchAndAddAsync("Billie Jean");

        await Expect(room.ActionError).ToContainTextAsync("3 tracks");
        await Expect(room.QueueItems).ToHaveCountAsync(2);
    });

    [Fact]
    public async Task RealTime_TrackAddedByOwner_AppearsForGuestWithoutReload()
    {
        var owner = await Fixture.CreateAccountAsync("own");
        var guest = await Fixture.CreateAccountAsync("gst");
        await RunAsync(async ownerPage =>
        {
            var ownerRoom = await (await LoginAsync(ownerPage, owner)).CreateRoomAsync("Live " + owner.Username, isPrivate: true);
            var code = (await ownerRoom.InviteCode.InnerTextAsync()).Replace("код ", string.Empty, StringComparison.Ordinal);
            await ownerRoom.SearchAndAddAsync("Numb");

            var guestContext = await NewContextAsync();
            try
            {
                var guestPage = await guestContext.NewPageAsync();
                var guestRoom = await (await LoginAsync(guestPage, guest)).JoinByCodeAsync(code);
                await Expect(guestRoom.Title).ToHaveTextAsync("Live " + owner.Username);
                await Expect(guestRoom.MyRole).ToHaveTextAsync("участник");

                await ownerRoom.SearchAndAddAsync("Despacito");

                await Expect(guestRoom.Item("Despacito")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 3_000 });
                await Expect(guestRoom.PlayingTitle).ToHaveTextAsync("Numb");
            }
            finally
            {
                await guestContext.CloseAsync();
            }
        });
    }

    [Fact]
    public Task PrivateRoom_WrongInviteCode_ShowsNotFound() => RunAsync(async page =>
    {
        var account = await Fixture.CreateAccountAsync();
        var rooms = await LoginAsync(page, account);

        await rooms.JoinByCodeAsync("ZZZZZZ");

        await Expect(rooms.Error).ToHaveTextAsync("Комната с таким кодом не найдена.");
    });

    [Fact]
    public Task RegularUser_OpensAdminPage_SeesForbiddenMessage() => RunAsync(async page =>
    {
        var account = await Fixture.CreateAccountAsync();
        await LoginAsync(page, account);
        var admin = new AdminUsersPage(page);

        await admin.OpenAsync();

        await Expect(admin.Forbidden).ToBeVisibleAsync();
    });

    [Fact]
    public Task Logout_ReturnsToLoginAndProtectsRooms() => RunAsync(async page =>
    {
        var account = await Fixture.CreateAccountAsync();
        var rooms = await LoginAsync(page, account);

        await rooms.LogoutAsync();
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login$"));
        await page.GotoAsync("");

        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login$"));
    });

    [Fact]
    public Task MobileViewport_RoomPage_HasNoHorizontalScroll() => RunAsync(
        async page =>
        {
            var account = await Fixture.CreateAccountAsync();
            var room = await (await LoginAsync(page, account)).CreateRoomAsync("Очень длинное название комнаты для проверки переноса на мобильном экране " + account.Username);
            await room.SearchAndAddAsync("Numb");
            await Expect(room.PlayingTitle).ToHaveTextAsync("Numb");

            var overflow = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

            overflow.ShouldBeLessThanOrEqualTo(0);
        },
        new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 375, Height = 812 } });

    private static async Task<RoomsPage> LoginAsync(IPage page, TestAccount account)
    {
        var login = new LoginPage(page);
        await login.OpenAsync();
        var rooms = await login.LoginAsync(account.Username, account.Password);
        await Expect(rooms.Heading).ToBeVisibleAsync();
        return rooms;
    }
}
