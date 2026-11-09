namespace Memoria.ToDo.Shared.Editing;

/// <summary>The list keys. Each returns null when the cursor is not in a list, so the key keeps its usual meaning.</summary>
public static class Lists
{
    public static TextAndSelection? Enter(TextAndSelection field)
    {
        var (text, start, length) = (field.Text, field.SelectionStart, field.SelectionLength);
        var lineStart = Lines.StartOf(text, start);
        var lineEnd = Lines.EndOf(text, start);
        if (!ListItem.TryParse(text.AsSpan(lineStart, lineEnd - lineStart), out var item))
        {
            return null;
        }

        var contentStart = lineStart + item.Indent + item.MarkerLength;
        if (start < contentStart)
        {
            return null;
        }

        if (length == 0 && text.AsSpan(contentStart, lineEnd - contentStart).IsWhiteSpace())
        {
            return new TextAndSelection(string.Concat(text.AsSpan(0, lineStart), text.AsSpan(lineEnd)), lineStart, 0);
        }

        var next = string.Concat("\n", new string(' ', item.Indent), item.NextMarker());
        return new TextAndSelection(
            string.Concat(text.AsSpan(0, start), next, text.AsSpan(start + length)),
            start + next.Length,
            0);
    }

    /// <summary>Moves the item in by the width of the marker of the item it nests under; unchanged when it has none.</summary>
    public static TextAndSelection? Indent(TextAndSelection field) =>
        Reindent(field, parentMustBeShallower: false, (item, parent) => item.Indent + parent.MarkerLength);

    /// <summary>Moves the item out to the indent of the item it nests under; unchanged at the left edge.</summary>
    public static TextAndSelection? Outdent(TextAndSelection field) =>
        Reindent(field, parentMustBeShallower: true, (_, parent) => parent.Indent);

    private static TextAndSelection? Reindent(
        TextAndSelection field, bool parentMustBeShallower, Func<ListItem, ListItem, int> newIndent)
    {
        var text = field.Text;
        var lineStart = Lines.StartOf(text, field.SelectionStart);
        var lineEnd = Lines.EndOf(text, field.SelectionStart);
        if (!ListItem.TryParse(text.AsSpan(lineStart, lineEnd - lineStart), out var item))
        {
            return null;
        }

        var hasParent = TryFindParent(text, lineStart, item.Indent, parentMustBeShallower, out var parent);
        if (!hasParent && !parentMustBeShallower)
        {
            return field;
        }

        var indent = hasParent ? newIndent(item, parent) : 0;
        if (indent == item.Indent)
        {
            return field;
        }

        var delta = indent - item.Indent;
        var start = Math.Max(lineStart, field.SelectionStart + delta);
        var end = Math.Max(start, field.SelectionStart + field.SelectionLength + delta);
        return new TextAndSelection(
            string.Concat(text.AsSpan(0, lineStart), new string(' ', indent), text.AsSpan(lineStart + item.Indent)),
            start,
            end - start);
    }

    /// <summary>
    /// Looks up from the line for the nearest item at or (when <paramref name="mustBeShallower"/>) before the given indent.
    /// Blank lines and indented lines don't end the search; a line of plain text at the left edge does.
    /// </summary>
    private static bool TryFindParent(string text, int lineStart, int indent, bool mustBeShallower, out ListItem parent)
    {
        parent = default;
        var position = lineStart;
        while (position > 0)
        {
            var previousStart = Lines.StartOf(text, position - 1);
            var previous = text.AsSpan(previousStart, position - 1 - previousStart);
            position = previousStart;

            if (previous.IsWhiteSpace())
            {
                continue;
            }

            if (ListItem.TryParse(previous, out var candidate))
            {
                if (mustBeShallower ? candidate.Indent < indent : candidate.Indent <= indent)
                {
                    parent = candidate;
                    return true;
                }
            }
            else if (previous[0] != ' ')
            {
                return false;
            }
        }

        return false;
    }
}
