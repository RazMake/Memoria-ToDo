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

    /// <summary>
    /// A key that sorts strictly between <paramref name="lower"/> and <paramref name="upper"/>; null means no limit on that side.
    /// Throws <see cref="ArgumentException"/> when <paramref name="lower"/> doesn't sort before <paramref name="upper"/>,
    /// and <see cref="InvalidDataException"/> for a key that isn't valid.
    /// </summary>
    public static string Between(string? lower, string? upper)
    {
        if (lower is null)
            return Before(upper);
        var lowerLength = IntegerLength(lower);
        if (upper is null)
            return Increment(lower[..lowerLength]) ?? lower[..lowerLength] + Midpoint(lower[lowerLength..], null);
        if (string.CompareOrdinal(lower, upper) >= 0)
            throw new ArgumentException("The lower key must sort before the upper key.", nameof(lower));

        var upperLength = IntegerLength(upper);
        var integer = lower[..lowerLength];
        if (integer == upper[..upperLength])
            return integer + Midpoint(lower[lowerLength..], upper[upperLength..]);
        var next = Increment(integer) ?? throw new InvalidDataException($"No rank sorts between '{lower}' and '{upper}'.");
        return string.CompareOrdinal(next, upper) < 0 ? next : integer + Midpoint(lower[lowerLength..], null);
    }

    /// <summary>A fraction that sorts strictly between <paramref name="lower"/> and <paramref name="upper"/>; null means no upper limit.</summary>
    private static string Midpoint(string lower, string? upper)
    {
        if (lower.EndsWith('0') || upper is not null && upper.EndsWith('0'))
            throw new InvalidDataException("A rank fraction can't end in 0.");
        if (upper is not null)
        {
            var common = 0;
            while (common < upper.Length && (common < lower.Length ? lower[common] : '0') == upper[common])
                common++;
            if (common > 0)
                return upper[..common] + Midpoint(lower[Math.Min(common, lower.Length)..], upper[common..]);
        }

        var low = lower.Length == 0 ? 0 : Digits.IndexOf(lower[0]);
        var high = upper is null ? Digits.Length : Digits.IndexOf(upper[0]);
        if (high - low > 1)
            return Digits[(low + high + 1) / 2].ToString();
        return upper is { Length: > 1 } ? upper[..1] : Digits[low] + Midpoint(lower.Length == 0 ? "" : lower[1..], null);
    }

    private static string? Increment(string integer)
    {
        var chars = integer.ToCharArray();
        for (var i = chars.Length - 1; i >= 1; i--)
        {
            if (chars[i] != 'z')
            {
                chars[i] = Digits[Digits.IndexOf(chars[i]) + 1];
                return new string(chars);
            }

            chars[i] = '0';
        }

        // Every digit carried: the whole-number part moves to the next head letter.
        var head = chars[0];
        if (head == 'Z')
            return "a0";
        if (head == 'z')
            return null;
        var next = (char)(head + 1);
        return next > 'a'
            ? next + new string(chars, 1, chars.Length - 1) + '0'
            : next + new string(chars, 1, chars.Length - 2);
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
