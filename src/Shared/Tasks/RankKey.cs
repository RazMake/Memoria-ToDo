using System.Buffers;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// Rank keys in the style of the fractional-indexing library: a head letter says how long the whole-number part is
/// ('a' is 2 characters, 'b' 3, ...; 'Z' is 2, 'Y' 3, ...), then base-62 digits that compare by character code.
/// </summary>
internal static class RankKey
{
    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly SearchValues<char> DigitChars = SearchValues.Create(Digits);

    /// <summary>A key that sorts before <paramref name="top"/>, the smallest key now in use; null when there is none.</summary>
    public static string Before(string? top)
    {
        if (top is null)
            return "a0";
        var length = IntegerLength(top);
        // A key with a fraction is above its own whole-number part.
        if (length < top.Length)
            return top[..length];
        return Decrement(top) ?? throw new InvalidDataException($"No rank sorts before '{top}'.");
    }

    private static int IntegerLength(string key)
    {
        var head = key.Length > 0 ? key[0] : '\0';
        var length = head switch
        {
            >= 'a' and <= 'z' => head - 'a' + 2,
            >= 'A' and <= 'Z' => 'Z' - head + 2,
            _ => int.MaxValue,
        };
        if (length > key.Length || key.AsSpan(1).ContainsAnyExcept(DigitChars))
            throw new InvalidDataException($"'{key}' isn't a rank key.");
        return length;
    }

    private static string? Decrement(string integer)
    {
        var chars = integer.ToCharArray();
        for (var i = chars.Length - 1; i >= 1; i--)
        {
            if (chars[i] != '0')
            {
                chars[i] = Digits[Digits.IndexOf(chars[i]) - 1];
                return new string(chars);
            }

            chars[i] = 'z';
        }

        // Every digit borrowed: the whole-number part moves to the previous head letter.
        var head = chars[0];
        if (head == 'a')
            return "Zz";
        if (head == 'A')
            return null;
        var previous = (char)(head - 1);
        return previous < 'Z'
            ? previous + new string(chars, 1, chars.Length - 1) + 'z'
            : previous + new string(chars, 1, chars.Length - 2);
    }
}
