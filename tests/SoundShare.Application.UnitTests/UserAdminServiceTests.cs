using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.Application.Validation;
using SoundShare.Domain.Users;

namespace SoundShare.Application.UnitTests;

public class UserAdminServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoomRepository _rooms = Substitute.For<IRoomRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UserAdminService _service;

    public UserAdminServiceTests()
    {
        var time = TestClock.Fixed();
        var rooms = new RoomService(_rooms, Substitute.For<IQueueRepository>(), _unitOfWork, time, Substitute.For<IRoomNotifier>(),
            new CreateRoomRequestValidator(), new JoinByCodeRequestValidator(), new BanRequestValidator());
        _service = new UserAdminService(_users, _unitOfWork, _hasher, time, rooms,
            new CreateUserRequestValidator(), new ChangeRoleRequestValidator(), new DeleteAccountRequestValidator());
    }

    [Fact]
    public async Task ChangeRole_LastActiveAdmin_ThrowsConflictAndKeepsRole()
    {
        var admin = Admin(1);
        _users.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(admin);
        _users.CountActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns(1);

        await Should.ThrowAsync<ConflictException>(() => _service.ChangeRoleAsync(1, new ChangeRoleRequest("User"), default));

        admin.Role.ShouldBe(SystemRole.Admin);
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task ChangeRole_AnotherAdminExists_DemotesAdmin()
    {
        var admin = Admin(1);
        _users.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(admin);
        _users.CountActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns(2);

        await _service.ChangeRoleAsync(1, new ChangeRoleRequest("User"), default);

        admin.Role.ShouldBe(SystemRole.User);
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task ChangeRole_UserToAdmin_DoesNotCountAdmins()
    {
        var user = Regular(2);
        _users.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(user);

        await _service.ChangeRoleAsync(2, new ChangeRoleRequest("Admin"), default);

        user.Role.ShouldBe(SystemRole.Admin);
        await _users.DidNotReceive().CountActiveAdminsAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Moderator")]
    [InlineData("admin")]
    [InlineData("root")]
    public async Task ChangeRole_UnknownRole_ThrowsValidation(string role)
    {
        await Should.ThrowAsync<ValidationException>(() => _service.ChangeRoleAsync(2, new ChangeRoleRequest(role), default));
    }

    [Fact]
    public async Task ChangeRole_UnknownUser_ThrowsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => _service.ChangeRoleAsync(404, new ChangeRoleRequest("User"), default));
    }

    [Fact]
    public async Task Block_LastActiveAdmin_ThrowsConflict()
    {
        var admin = Admin(1);
        _users.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(admin);
        _users.CountActiveAdminsAsync(Arg.Any<CancellationToken>()).Returns(1);

        await Should.ThrowAsync<ConflictException>(() => _service.BlockAsync(1, default));

        admin.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Block_RegularUser_DeactivatesAndRevokesRefreshTokens()
    {
        var user = Regular(2);
        var token = new RefreshToken(2, [1], TestClock.Now.AddHours(-1), TestClock.Now.AddDays(6), null);
        _users.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(user);
        _users.GetActiveRefreshTokensAsync(2, TestClock.Now, Arg.Any<CancellationToken>()).Returns([token]);

        await _service.BlockAsync(2, default);

        user.IsActive.ShouldBeFalse();
        token.IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Unblock_BlockedUser_ActivatesUser()
    {
        var user = Regular(2);
        user.Block();
        _users.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(user);

        await _service.UnblockAsync(2, default);

        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ValidAdmin_StoresUserWithRequestedRole()
    {
        _hasher.Hash("Secret_123").Returns("hashed");

        var result = await _service.CreateAsync(new CreateUserRequest("new_admin", "admin2@example.com", "Secret_123", "Admin"), default);

        result.Role.ShouldBe("Admin");
        _users.Received(1).Add(Arg.Is<User>(u => u.Role == SystemRole.Admin && u.PasswordHash == "hashed"));
    }

    [Fact]
    public async Task Create_DuplicateUsername_ThrowsConflict()
    {
        _users.UsernameExistsAsync("user_1", Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<ConflictException>(() => _service.CreateAsync(new CreateUserRequest("user_1", "x@example.com", "Secret_123", "User"), default));
    }

    [Fact]
    public async Task Get_UnknownUser_ThrowsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => _service.GetAsync(404, default));
    }

    [Fact]
    public async Task List_OutOfRangePaging_ClampsPageAndPageSize()
    {
        _users.ListAsync(null, null, 1, 100, Arg.Any<CancellationToken>()).Returns(new PagedResult<User>([Regular(2)], 1, 1, 100));

        var result = await _service.ListAsync(null, null, 0, 1000, default);

        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(100);
        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteOwnAccount_WrongPassword_ThrowsForbidden()
    {
        var user = Regular(2);
        _users.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("hash", "Wrong_123").Returns(false);

        await Should.ThrowAsync<ForbiddenException>(() => _service.DeleteOwnAccountAsync(2, new DeleteAccountRequest("Wrong_123"), default));

        user.Username.ShouldBe("user_2");
    }

    [Fact]
    public async Task DeleteOwnAccount_CorrectPassword_AnonymizesUserAndRevokesTokens()
    {
        var user = Regular(2);
        var token = new RefreshToken(2, [1], TestClock.Now.AddHours(-1), TestClock.Now.AddDays(6), null);
        _users.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(user);
        _users.GetActiveRefreshTokensAsync(2, TestClock.Now, Arg.Any<CancellationToken>()).Returns([token]);
        _hasher.Verify("hash", "Secret_123").Returns(true);

        await _service.DeleteOwnAccountAsync(2, new DeleteAccountRequest("Secret_123"), default);

        user.Username.ShouldBe("deleted_2");
        user.Email.ShouldEndWith(".invalid");
        user.IsActive.ShouldBeFalse();
        token.IsRevoked.ShouldBeTrue();
        _unitOfWork.TransactionCount.ShouldBe(1);
    }

    private static User Admin(long id) =>
        new User($"admin_{id}", $"admin_{id}@example.com", "hash", SystemRole.Admin, TestClock.Now).WithId(id);

    private static User Regular(long id) =>
        new User($"user_{id}", $"user_{id}@example.com", "hash", SystemRole.User, TestClock.Now).WithId(id);
}
