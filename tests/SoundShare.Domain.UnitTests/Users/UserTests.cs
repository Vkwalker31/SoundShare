using SoundShare.Domain.Users;

namespace SoundShare.Domain.UnitTests.Users;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Anonymize_ActiveUser_RemovesPersonalDataAndDeactivates()
    {
        var user = new User("user_deleted", "user_deleted@example.com", "hash", SystemRole.User, Now);

        user.Anonymize("!unusable");

        user.Username.ShouldStartWith("deleted_");
        user.Email.ShouldEndWith("@deleted.invalid");
        user.PasswordHash.ShouldBe("!unusable");
        user.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Block_ThenUnblock_RestoresActivity()
    {
        var user = new User("user_1", "user_1@example.com", "hash", SystemRole.User, Now);

        user.Block();
        user.IsActive.ShouldBeFalse();
        user.Unblock();

        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void RefreshToken_Revoke_IsIdempotentAndDeactivates()
    {
        var token = new RefreshToken(1, new byte[32], Now, Now.AddDays(7), "127.0.0.1");

        token.Revoke(Now.AddMinutes(1), 42);
        token.Revoke(Now.AddMinutes(2), 99);

        token.IsActive(Now.AddMinutes(3)).ShouldBeFalse();
        token.RevokedAt.ShouldBe(Now.AddMinutes(1));
        token.ReplacedByTokenId.ShouldBe(42);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7 * 24 * 60 - 1, true)]
    [InlineData(7 * 24 * 60, false)]
    public void RefreshToken_IsActive_DependsOnExpiry(int minutesLater, bool expected)
    {
        var token = new RefreshToken(1, new byte[32], Now, Now.AddDays(7), null);

        token.IsActive(Now.AddMinutes(minutesLater)).ShouldBe(expected);
    }
}
