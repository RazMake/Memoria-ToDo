namespace Memoria.ToDo.Shared.Editing;

/// <summary>Typing "/" in the Description: when the picker opens, what it lists, and what choosing a snippet does.</summary>
public static class SnippetPicker
{
    /// <summary>True when the character at <paramref name="slashIndex"/> is a "/" at a line start or after a space.</summary>
    public static bool OpensAt(string text, int slashIndex)
    {
        ArgumentNullException.ThrowIfNull(text);
        return (uint)slashIndex < (uint)text.Length
            && text[slashIndex] == '/'
            && (slashIndex == 0 || text[slashIndex - 1] is ' ' or '\n');
    }

    /// <summary>The snippets that match the text between the "/" and the cursor.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slashIndex"/> is not a "/" before the cursor.</exception>
    public static IReadOnlyList<ISnippet> Matching(TextAndSelection field, int slashIndex)
    {
        ArgumentNullException.ThrowIfNull(field);
        var typed = Typed(field, slashIndex);
        if (typed.IsEmpty)
        {
            return Snippets.All;
        }

        var matches = new List<ISnippet>();
        foreach (var snippet in Snippets.All)
        {
            if (AnyWordStartsWith(snippet.Name, typed))
            {
                matches.Add(snippet);
            }
        }

        return matches;
    }

    /// <summary>Inserts <paramref name="snippet"/> in place of the "/" and the text typed after it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slashIndex"/> is not a "/" before the cursor.</exception>
    public static TextAndSelection Insert(TextAndSelection field, int slashIndex, ISnippet snippet, SnippetInput input)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(snippet);
        Typed(field, slashIndex);

        var replaced = new TextAndSelection(
            field.Text, slashIndex, field.SelectionStart + field.SelectionLength - slashIndex);
        return snippet.Insert(replaced, input);
    }

    /// <summary>The text between the "/" and the cursor; throws when the index isn't a "/" before the cursor.</summary>
    private static ReadOnlySpan<char> Typed(TextAndSelection field, int slashIndex)
    {
        if (slashIndex < 0 || slashIndex >= field.SelectionStart || field.Text[slashIndex] != '/')
        {
            throw new ArgumentOutOfRangeException(nameof(slashIndex), "The index must be a \"/\" before the cursor.");
        }

        return field.Text.AsSpan(slashIndex + 1, field.SelectionStart - slashIndex - 1);
    }

    private static bool AnyWordStartsWith(string name, ReadOnlySpan<char> typed)
    {
        for (var i = 0; i < name.Length; i++)
        {
            if ((i == 0 || name[i - 1] == ' ')
                && name.AsSpan(i).StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
