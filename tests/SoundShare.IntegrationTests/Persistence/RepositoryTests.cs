using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.Domain.Audit;
using SoundShare.Domain.Catalog;
using SoundShare.Domain.Users;
using SoundShare.IntegrationTests.Infrastructure;

namespace SoundShare.IntegrationTests.Persistence;

[Collection(SoundShareTestGroup.Name)]
public class RepositoryTests(SoundShareFixture fixture)
{
    [Fact]
    public async Task Users_GetByUsername_IgnoresCase()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var username = TestApi.UniqueName("Mx");
        users.Add(NewUser(username));
        await unitOfWork.SaveChangesAsync(default);

        var found = await users.GetByUsernameAsync(username.ToLowerInvariant(), default);

        found.ShouldNotBeNull().Username.ShouldBe(username);
        (await users.UsernameExistsAsync(username.ToUpperInvariant(), default)).ShouldBeTrue();
    }

    [Fact]
    public async Task Users_DuplicateEmailDifferentCase_ThrowsUniqueViolation()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var email = $"{TestApi.UniqueName("e")}@example.com";
        users.Add(new User(TestApi.UniqueName("d1"), email, "hash", SystemRole.User, DateTime.UtcNow));
        await unitOfWork.SaveChangesAsync(default);

        users.Add(new User(TestApi.UniqueName("d2"), email.ToUpperInvariant(), "hash", SystemRole.User, DateTime.UtcNow));
        var error = await Should.ThrowAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync(default));

        error.InnerException.ShouldBeOfType<Oracle.ManagedDataAccess.Client.OracleException>().Number.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshTokens_StoredAsRawHash_FoundByHash()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var user = NewUser(TestApi.UniqueName("rt"));
        users.Add(user);
        await unitOfWork.SaveChangesAsync(default);
        var hash = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var now = DateTime.UtcNow;
        users.AddRefreshToken(new RefreshToken(user.Id, hash, now, now.AddDays(7), "127.0.0.1"));
        await unitOfWork.SaveChangesAsync(default);

        using var readScope = fixture.Factory.Services.CreateScope();
        var reader = readScope.ServiceProvider.GetRequiredService<IUserRepository>();
        var found = await reader.GetRefreshTokenAsync(hash.ToArray(), default);
        var active = await reader.GetActiveRefreshTokensAsync(user.Id, now, default);

        found.ShouldNotBeNull().UserId.ShouldBe(user.Id);
        active.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Queue_VotesFromSeveralUsers_ScoreIsSumOfVotes()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var users = services.GetRequiredService<IUserRepository>();
        var catalog = services.GetRequiredService<ICatalogRepository>();
        var queues = services.GetRequiredService<IQueueRepository>();
        var accounts = Enumerable.Range(0, 4).Select(_ => NewUser(TestApi.UniqueName("v"))).ToList();
        accounts.ForEach(users.Add);
        var first = new Track("First", 200, TestApi.NewVideoId(), DateTime.UtcNow);
        var second = new Track("Second", 200, TestApi.NewVideoId(), DateTime.UtcNow);
        catalog.AddTrack(first);
        catalog.AddTrack(second);
        await unitOfWork.SaveChangesAsync(default);

        var rooms = services.GetRequiredService<RoomService>();
        var room = await rooms.CreateAsync(accounts[0].Id, new CreateRoomRequest("Votes", false), default);
        foreach (var member in accounts.Skip(1))
        {
            await rooms.JoinPublicAsync(room.Id, member.Id, default);
        }

        var queue = services.GetRequiredService<QueueService>();
        await queue.AddItemAsync(room.Id, accounts[0].Id, new AddQueueItemRequest(first.Id), default);
        var item = await queue.AddItemAsync(room.Id, accounts[1].Id, new AddQueueItemRequest(second.Id), default);
        await queue.VoteAsync(room.Id, accounts[2].Id, item.Id, new VoteRequest(-1), default);
        await queue.VoteAsync(room.Id, accounts[3].Id, item.Id, new VoteRequest(1), default);
        await queue.VoteAsync(room.Id, accounts[3].Id, item.Id, new VoteRequest(-1), default);

        (await queues.GetScoreAsync(item.Id, default)).ShouldBe(-1);
        (await queues.GetVoteAsync(item.Id, accounts[3].Id, default)).ShouldNotBeNull().VoteValue.ShouldBe(-1);
        var playlist = await queues.GetRoomQueueAsync(room.Id, default);
        (await queues.CountActiveItemsByUserAsync(playlist!.Id, accounts[1].Id, default)).ShouldBe(1);
    }

    [Fact]
    public async Task Rooms_ListPublic_ReturnsActiveRoomsWithMemberCount()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<IUserRepository>();
        var owner = NewUser(TestApi.UniqueName("o"));
        var guest = NewUser(TestApi.UniqueName("g"));
        users.Add(owner);
        users.Add(guest);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        var rooms = services.GetRequiredService<RoomService>();
        var publicRoom = await rooms.CreateAsync(owner.Id, new CreateRoomRequest("Public list", false), default);
        await rooms.JoinPublicAsync(publicRoom.Id, guest.Id, default);
        var privateRoom = await rooms.CreateAsync(guest.Id, new CreateRoomRequest("Private list", true), default);

        var list = await services.GetRequiredService<IRoomRepository>().ListPublicAsync(default);

        list.ShouldContain(r => r.Room.Id == publicRoom.Id && r.MembersCount == 1);
        list.ShouldNotContain(r => r.Room.Id == privateRoom.Id);
    }

    [Fact]
    public async Task Rooms_LockInsideTransaction_Succeeds()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var owner = NewUser(TestApi.UniqueName("l"));
        services.GetRequiredService<IUserRepository>().Add(owner);
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        await unitOfWork.SaveChangesAsync(default);
        var room = await services.GetRequiredService<RoomService>().CreateAsync(owner.Id, new CreateRoomRequest("Lock", false), default);
        var repository = services.GetRequiredService<IRoomRepository>();

        var members = await unitOfWork.InTransactionAsync(
            async ct =>
            {
                await repository.LockAsync(room.Id, ct);
                return await repository.CountActiveMembersAsync(room.Id, ct);
            },
            default);

        members.ShouldBe(1);
    }

    [Fact]
    public async Task Audit_AddRangeAndFilterByAction_ReturnsNewestFirst()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditRepository>();
        var action = TestApi.UniqueName("Act");
        var now = DateTime.UtcNow;
        await audit.AddRangeAsync(
        [
            new AuditLog { CreatedAt = now.AddSeconds(-2), Source = "SYSTEM", Action = action, Details = "{\"n\":1}" },
            new AuditLog { CreatedAt = now.AddSeconds(-1), Source = "SYSTEM", Action = action, Details = "{\"n\":2}" },
        ],
            default);

        var logs = await audit.ListAsync(null, action, null, null, null, 10, default);

        logs.Count.ShouldBe(2);
        logs[0].Id.ShouldBeGreaterThan(logs[1].Id);
    }

    [Fact]
    public async Task Catalog_SearchByTitle_IsCaseInsensitive()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
        var marker = TestApi.UniqueName("Song");
        catalog.AddTrack(new Track($"{marker} Remastered", 180, TestApi.NewVideoId(), DateTime.UtcNow));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);

        var found = await catalog.SearchAsync(marker.ToLowerInvariant(), 20, default);

        found.ShouldHaveSingleItem().Title.ShouldStartWith(marker);
    }

    private static User NewUser(string username) =>
        new(username, $"{Guid.NewGuid():N}@example.com", "hash", SystemRole.User, DateTime.UtcNow);
}
