using SoundShare.Domain.Rooms;

namespace SoundShare.Domain.UnitTests.Rooms;

public class InviteCodeTests
{
    [Theory]
    [InlineData("ABCDEF", "ABCDEF")]
    [InlineData("rckbar", "RCKBAR")]
    [InlineData("  JZZ7Q2 ", "JZZ7Q2")]
    public void TryParse_ValidInput_ReturnsNormalizedCode(string input, string expected)
    {
        var parsed = InviteCode.TryParse(input, out var code);

        parsed.ShouldBeTrue();
        code.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC")]
    [InlineData("ABCDEFG")]
    [InlineData("AB0CDE")]
    [InlineData("ABCDE1")]
    [InlineData("ABCDEI")]
    [InlineData("ABCDEO")]
    [InlineData("ABC-EF")]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        var parsed = InviteCode.TryParse(input, out _);

        parsed.ShouldBeFalse();
    }

    [Fact]
    public void Generate_ManyTimes_AlwaysProducesParsableCodes()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => InviteCode.Generate().Value).ToList();

        codes.All(c => InviteCode.TryParse(c, out _)).ShouldBeTrue();
        codes.Distinct().Count().ShouldBeGreaterThan(490);
    }
}
