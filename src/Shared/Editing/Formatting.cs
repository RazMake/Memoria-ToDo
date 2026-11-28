namespace Memoria.ToDo.Shared.Editing;

/// <summary>The commands that wrap the selection in Markdown, or remove it again.</summary>
public static class Formatting
{
    private const int MaxHeadingLevel = 3;

    public static TextAndSelection Bold(TextAndSelection field) => Wrapping.Toggle(field, "**", "**");

    public static TextAndSelection Italic(TextAndSelection field) => Wrapping.Toggle(field, "*", "*");

    public static TextAndSelection Strikethrough(TextAndSelection field) => Wrapping.Toggle(field, "~~", "~~");

    public static TextAndSelection InlineCode(TextAndSelection field) => Wrapping.Toggle(field, "`", "`");

    /// <exception cref="ArgumentException"><paramref name="color"/> is not in <paramref name="paletteNames"/>.</exception>
    public static TextAndSelection Color(TextAndSelection field, string color, IReadOnlyCollection<string> paletteNames)
    {
        ArgumentNullException.ThrowIfNull(color);
        ArgumentNullException.ThrowIfNull(paletteNames);
        if (!Contains(paletteNames, color))
        {
            throw new ArgumentException($"'{color}' is not one of the palette's colors.", nameof(color));
        }

        var open = $":{color}[";
        var existing = OwnColorLength(field, paletteNames);
        if (existing == 0)
        {
            return Wrapping.Toggle(field, open, "]");
        }

        var sameColor = existing == open.Length
            && field.Text.AsSpan(field.SelectionStart - existing, existing).SequenceEqual(open);
        return sameColor
            ? Wrapping.Unwrap(field, existing, 1)
            : Wrapping.ReplaceOpen(field, existing, open);
    }

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not 1, 2, or 3.</exception>
    public static TextAndSelection Heading(TextAndSelection field, int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, MaxHeadingLevel);

        var (text, start, length) = (field.Text, field.SelectionStart, field.SelectionLength);
        var lineStart = Lines.StartOf(text, start);
        var oldLength = HeadingPrefixLength(text.AsSpan(lineStart), out var oldLevel);
        var newPrefix = oldLevel == level ? string.Empty : string.Concat(new string('#', level), " ");

        var delta = newPrefix.Length - oldLength;
        var newStart = Math.Max(lineStart + newPrefix.Length, start + delta);
        var newEnd = Math.Max(newStart, start + length + delta);
        return new TextAndSelection(
            string.Concat(text.AsSpan(0, lineStart), newPrefix, text.AsSpan(lineStart + oldLength)),
            newStart,
            newEnd - newStart);
    }

    /// <summary>The length of "#… " at the start of the line, and its level; 0 when the line is not a heading.</summary>
    private static int HeadingPrefixLength(ReadOnlySpan<char> line, out int level)
    {
        level = 0;
        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        if (level == 0 || level > 6 || level >= line.Length || line[level] != ' ')
        {
            level = 0;
            return 0;
        }

        return level + 1;
    }

    /// <summary>The length of ":name[" right before the selection when "]" follows it and name is a palette color; otherwise 0.</summary>
    private static int OwnColorLength(TextAndSelection field, IReadOnlyCollection<string> paletteNames)
    {
        var text = field.Text;
        var before = text.AsSpan(0, field.SelectionStart);
        if (!before.EndsWith("[", StringComparison.Ordinal)
            || !text.AsSpan(field.SelectionStart + field.SelectionLength).StartsWith("]", StringComparison.Ordinal))
        {
            return 0;
        }

        var colon = before[..^1].LastIndexOf(':');
        if (colon < 0)
        {
            return 0;
        }

        var name = before[(colon + 1)..^1];
        foreach (var palette in paletteNames)
        {
            if (name.SequenceEqual(palette))
            {
                return before.Length - colon;
            }
        }

        return 0;
    }

    private static bool Contains(IReadOnlyCollection<string> names, string name)
    {
        foreach (var candidate in names)
        {
            if (string.Equals(candidate, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
