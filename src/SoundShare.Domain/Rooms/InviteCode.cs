using System.Security.Cryptography;

namespace SoundShare.Domain.Rooms;

public readonly record struct InviteCode
{
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 6;

    private InviteCode(string value) => Value = value;

    public string Value { get; }

    public static InviteCode Generate()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new InviteCode(new string(chars));
    }

    public static bool TryParse(string? input, out InviteCode code)
    {
        code = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim().ToUpperInvariant();
        if (normalized.Length != Length || normalized.Any(c => !Alphabet.Contains(c, StringComparison.Ordinal)))
        {
            return false;
        }

        code = new InviteCode(normalized);
        return true;
    }

    public override string ToString() => Value;
}
