using FluentValidation;
using SoundShare.Application.Abstractions;
using SoundShare.Application.Common;
using SoundShare.Application.Contracts;
using SoundShare.Application.Services;
using SoundShare.Application.Validation;
using SoundShare.Domain.Users;

namespace SoundShare.Application.UnitTests;

public class AuthServiceTests
{
    private static readonly byte[] OldHash = Enumerable.Repeat((byte)1, 32).ToArray();
    private static readonly byte[] NewHash = Enumerable.Repeat((byte)2, 32).ToArray();

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(_users, _unitOfWork, _hasher, _tokens, TestClock.Fixed(), new RegisterRequestValidator(), new LoginRequestValidator());
        _tokens.CreateAccessToken(Arg.Any<User>()).Returns(new AccessToken("access", 900));
        _tokens.GenerateRefreshToken().Returns(new GeneratedRefreshToken("new-refresh", NewHash));
        _tokens.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));
        _tokens.HashRefreshToken("new-refresh").Returns(NewHash);
        _tokens.HashRefreshToken("old-refresh").Returns(OldHash);
    }

    [Fact]
    public async Task Register_ValidRequest_StoresUserWithHashedPassword()
    {
        _hasher.Hash("Secret_123").Returns("hashed");

        var result = await _service.RegisterAsync(new RegisterRequest("user_1", "user_1@example.com", "Secret_123"), default);

        result.Username.ShouldBe("user_1");
        result.Role.ShouldBe("User");
        _users.Received(1).Add(Arg.Is<User>(u => u.PasswordHash == "hashed" && u.Role == SystemRole.User && u.IsActive));
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Register_UsernameTaken_ThrowsConflict()
    {
        _users.UsernameExistsAsync("user_1", Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<ConflictException>(() => _service.RegisterAsync(new RegisterRequest("user_1", "new@example.com", "Secret_123"), default));

        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Register_EmailTaken_ThrowsConflict()
    {
        _users.EmailExistsAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<ConflictException>(() => _service.RegisterAsync(new RegisterRequest("user_9", "taken@example.com", "Secret_123"), default));
    }

    [Theory]
    [InlineData("ab", "a@example.com", "Secret_123")]
    [InlineData("user 1", "a@example.com", "Secret_123")]
    [InlineData("user_1", "not-an-email", "Secret_123")]
    [InlineData("user_1", "a@example.com", "Short1")]
    [InlineData("user_1", "a@example.com", "OnlyLetters")]
    [InlineData("user_1", "a@example.com", "12345678")]
    public async Task Register_InvalidData_ThrowsValidationAndStoresNothing(string username, string email, string password)
    {
        await Should.ThrowAsync<ValidationException>(() => _service.RegisterAsync(new RegisterRequest(username, email, password), default));

        _users.DidNotReceive().Add(Arg.Any<User>());
        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Login_UnknownUser_ThrowsSameErrorAsWrongPassword()
    {
        var unknown = await Should.ThrowAsync<AuthenticationFailedException>(() => _service.LoginAsync(new LoginRequest("ghost", "Secret_123"), null, default));

        _users.GetByUsernameAsync("user_1", Arg.Any<CancellationToken>()).Returns(ActiveUser());
        _hasher.Verify("hash", "Wrong_123").Returns(false);
        var wrongPassword = await Should.ThrowAsync<AuthenticationFailedException>(() => _service.LoginAsync(new LoginRequest("user_1", "Wrong_123"), null, default));

        unknown.Message.ShouldBe(wrongPassword.Message);
    }

    [Fact]
    public async Task Login_BlockedUser_ThrowsAccountBlocked()
    {
        var user = ActiveUser();
        user.Block();
        _users.GetByUsernameAsync("user_1", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("hash", "Secret_123").Returns(true);

        await Should.ThrowAsync<AccountBlockedException>(() => _service.LoginAsync(new LoginRequest("user_1", "Secret_123"), null, default));

        _users.DidNotReceive().AddRefreshToken(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Login_ValidCredentials_IssuesAccessAndRefreshTokens()
    {
        _users.GetByUsernameAsync("user_1", Arg.Any<CancellationToken>()).Returns(ActiveUser());
        _hasher.Verify("hash", "Secret_123").Returns(true);

        var result = await _service.LoginAsync(new LoginRequest("user_1", "Secret_123"), "127.0.0.1", default);

        result.AccessToken.ShouldBe("access");
        result.ExpiresIn.ShouldBe(900);
        result.RefreshToken.ShouldBe("new-refresh");
        result.RefreshTokenExpiresAt.ShouldBe(TestClock.Now.AddDays(7));
        _users.Received(1).AddRefreshToken(Arg.Is<RefreshToken>(t =>
            t.UserId == 5 && t.TokenHash == NewHash && t.CreatedByIp == "127.0.0.1" && t.ExpiresAt == TestClock.Now.AddDays(7)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refresh_MissingToken_ThrowsAuthenticationFailed(string? token)
    {
        await Should.ThrowAsync<AuthenticationFailedException>(() => _service.RefreshAsync(token, null, default));
    }

    [Fact]
    public async Task Refresh_UnknownToken_ThrowsAuthenticationFailed()
    {
        await Should.ThrowAsync<AuthenticationFailedException>(() => _service.RefreshAsync("old-refresh", null, default));
    }

    [Fact]
    public async Task Refresh_RevokedTokenReused_RevokesAllActiveTokensOfUser()
    {
        var stolen = new RefreshToken(5, OldHash, TestClock.Now.AddDays(-1), TestClock.Now.AddDays(6), null);
        stolen.Revoke(TestClock.Now.AddHours(-1));
        var first = new RefreshToken(5, NewHash, TestClock.Now.AddHours(-1), TestClock.Now.AddDays(6), null);
        var second = new RefreshToken(5, [3], TestClock.Now.AddMinutes(-5), TestClock.Now.AddDays(6), null);
        _users.GetRefreshTokenAsync(OldHash, Arg.Any<CancellationToken>()).Returns(stolen);
        _users.GetActiveRefreshTokensAsync(5, TestClock.Now, Arg.Any<CancellationToken>()).Returns([first, second]);

        var error = await Should.ThrowAsync<AuthenticationFailedException>(() => _service.RefreshAsync("old-refresh", null, default));

        error.Message.ShouldContain("reuse");
        first.IsRevoked.ShouldBeTrue();
        second.IsRevoked.ShouldBeTrue();
        _users.DidNotReceive().AddRefreshToken(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ThrowsAuthenticationFailed()
    {
        var expired = new RefreshToken(5, OldHash, TestClock.Now.AddDays(-8), TestClock.Now.AddMinutes(-1), null);
        _users.GetRefreshTokenAsync(OldHash, Arg.Any<CancellationToken>()).Returns(expired);
        _users.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(ActiveUser());

        await Should.ThrowAsync<AuthenticationFailedException>(() => _service.RefreshAsync("old-refresh", null, default));
    }

    [Fact]
    public async Task Refresh_BlockedUser_ThrowsAuthenticationFailed()
    {
        var user = ActiveUser();
        user.Block();
        _users.GetRefreshTokenAsync(OldHash, Arg.Any<CancellationToken>()).Returns(ActiveToken());
        _users.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(user);

        await Should.ThrowAsync<AuthenticationFailedException>(() => _service.RefreshAsync("old-refresh", null, default));
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesTokenInsideTransaction()
    {
        var stored = ActiveToken();
        var replacement = new RefreshToken(5, NewHash, TestClock.Now, TestClock.Now.AddDays(7), null).WithId(77);
        _users.GetRefreshTokenAsync(OldHash, Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetRefreshTokenAsync(NewHash, Arg.Any<CancellationToken>()).Returns(replacement);
        _users.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(ActiveUser());

        var result = await _service.RefreshAsync("old-refresh", null, default);

        result.RefreshToken.ShouldBe("new-refresh");
        stored.IsRevoked.ShouldBeTrue();
        stored.ReplacedByTokenId.ShouldBe(77);
        _unitOfWork.TransactionCount.ShouldBe(1);
    }

    [Fact]
    public async Task Logout_KnownToken_RevokesIt()
    {
        var stored = ActiveToken();
        _users.GetRefreshTokenAsync(OldHash, Arg.Any<CancellationToken>()).Returns(stored);

        await _service.LogoutAsync("old-refresh", default);

        stored.IsRevoked.ShouldBeTrue();
        _unitOfWork.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Logout_UnknownToken_DoesNothing()
    {
        await _service.LogoutAsync("old-refresh", default);

        _unitOfWork.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task LogoutAll_ActiveTokens_RevokesEveryToken()
    {
        var first = ActiveToken();
        var second = new RefreshToken(5, NewHash, TestClock.Now.AddHours(-2), TestClock.Now.AddDays(5), null);
        _users.GetActiveRefreshTokensAsync(5, TestClock.Now, Arg.Any<CancellationToken>()).Returns([first, second]);

        await _service.LogoutAllAsync(5, default);

        first.IsRevoked.ShouldBeTrue();
        second.IsRevoked.ShouldBeTrue();
    }

    private static User ActiveUser() =>
        new User("user_1", "user_1@example.com", "hash", SystemRole.User, TestClock.Now.AddDays(-30)).WithId(5);

    private static RefreshToken ActiveToken() =>
        new(5, OldHash, TestClock.Now.AddDays(-1), TestClock.Now.AddDays(6), null);
}
