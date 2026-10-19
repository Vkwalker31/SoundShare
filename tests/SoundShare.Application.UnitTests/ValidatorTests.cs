using SoundShare.Application.Contracts;
using SoundShare.Application.Validation;

namespace SoundShare.Application.UnitTests;

public class ValidatorTests
{
    [Theory]
    [InlineData("abc", true)]
    [InlineData("ab", false)]
    [InlineData("user_name_32_characters_long_xxx", true)]
    [InlineData("user_name_33_characters_long_xxxx", false)]
    [InlineData("user-name", false)]
    [InlineData("юзер", false)]
    public void Register_UsernameBoundaries(string username, bool valid)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest(username, "a@example.com", "Secret_123"));

        result.IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData("Abcdef12", true)]
    [InlineData("Abcde12", false)]
    [InlineData("abcdefgh", false)]
    [InlineData("12345678", false)]
    [InlineData("", false)]
    public void Register_PasswordRules(string password, bool valid)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("user_1", "a@example.com", password));

        result.IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(-2, false)]
    public void Vote_AllowedValues(int value, bool valid)
    {
        new VoteRequestValidator().Validate(new VoteRequest(value)).IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(525_600, true)]
    [InlineData(0, false)]
    [InlineData(525_601, false)]
    [InlineData(null, true)]
    public void Ban_DurationBoundaries(int? minutes, bool valid)
    {
        new BanRequestValidator().Validate(new BanRequest(5, minutes)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Ban_NonPositiveUserId_IsInvalid()
    {
        new BanRequestValidator().Validate(new BanRequest(0, 10)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Room", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void CreateRoom_NameRules(string name, bool valid)
    {
        new CreateRoomRequestValidator().Validate(new CreateRoomRequest(name, false)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void CreateRoom_NameLongerThan100_IsInvalid()
    {
        new CreateRoomRequestValidator().Validate(new CreateRoomRequest(new string('x', 101), false)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("User", true)]
    [InlineData("Moderator", false)]
    public void CreateUser_RoleRules(string role, bool valid)
    {
        new CreateUserRequestValidator().Validate(new CreateUserRequest("user_1", "a@example.com", "Secret_123", role)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Login_EmptyFields_AreInvalid()
    {
        new LoginRequestValidator().Validate(new LoginRequest("", "")).Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void Playlist_EmptyName_IsInvalid()
    {
        new CreatePlaylistRequestValidator().Validate(new CreatePlaylistRequest(" ", null)).IsValid.ShouldBeFalse();
    }
}
