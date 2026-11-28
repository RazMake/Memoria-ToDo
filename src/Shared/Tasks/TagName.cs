using System.Text;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The one rule that turns text into a tag.</summary>
public static class TagName
{
    /// <summary>The most tags one task has.</summary>
    public const int MaxPerTask = 15;

    /// <summary>
    /// <paramref name="text"/> trimmed, lowercase by the invariant culture's rules, with each run of whitespace,
    /// line breaks included, replaced by one hyphen. Empty when <paramref name="text"/> holds only whitespace.
    /// </summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.AsSpan().Trim();
        var tag = new StringBuilder(trimmed.Length);
        var inGap = false;
        foreach (var letter in trimmed.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(letter))
            {
                if (!inGap)
                    tag.Append('-');
                inGap = true;
            }
            else
            {
                tag.Append(Rune.ToLowerInvariant(letter).ToString());
                inGap = false;
            }
        }

        return tag.ToString();
    }
}
