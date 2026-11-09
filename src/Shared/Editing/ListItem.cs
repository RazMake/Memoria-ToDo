namespace Memoria.ToDo.Shared.Editing;

/// <summary>A bulleted or numbered list item's leading spaces and marker, read from the start of a line.</summary>
internal readonly struct ListItem
{
    private const int MaxDigits = 9;

    private ListItem(int indent, int markerLength, int number, char markerChar)
    {
        Indent = indent;
        MarkerLength = markerLength;
        Number = number;
        MarkerChar = markerChar;
    }

    /// <summary>The count of spaces before the marker.</summary>
    public int Indent { get; }

    /// <summary>The width of the marker with the space after it, such as 2 for "- " and 3 for "1. ".</summary>
    public int MarkerLength { get; }

    /// <summary>The item's number, or -1 for a bullet.</summary>
    public int Number { get; }

    /// <summary>The bullet character, or the "." or ")" after a number.</summary>
    public char MarkerChar { get; }

    /// <summary>The marker, with the space after it, of the item that follows this one.</summary>
    public string NextMarker() =>
        Number < 0 ? $"{MarkerChar} " : $"{Number + 1}{MarkerChar} ";

    /// <summary>Reads the item at the start of <paramref name="line"/>; false when the line is not a list item.</summary>
    public static bool TryParse(ReadOnlySpan<char> line, out ListItem item)
    {
        item = default;
        var indent = 0;
        while (indent < line.Length && line[indent] == ' ')
        {
            indent++;
        }

        var rest = line[indent..];
        if (rest.Length >= 2 && rest[0] is '-' or '*' or '+' && rest[1] == ' ')
        {
            item = new ListItem(indent, 2, -1, rest[0]);
            return true;
        }

        var digits = 0;
        while (digits < rest.Length && digits <= MaxDigits && char.IsAsciiDigit(rest[digits]))
        {
            digits++;
        }

        if (digits is > 0 and <= MaxDigits
            && digits + 1 < rest.Length
            && rest[digits] is '.' or ')'
            && rest[digits + 1] == ' ')
        {
            item = new ListItem(indent, digits + 2, int.Parse(rest[..digits]), rest[digits]);
            return true;
        }

        return false;
    }
}
