namespace Memoria.ToDo.Shared.Editing;

/// <summary>Typing "@" in the Description: when the mention list opens, what was typed, and what choosing a contact does.</summary>
public static class MentionPicker
{
    /// <summary>True when the character at <paramref name="atIndex"/> is an "@" at the start of the text or after a character that isn't a letter or a digit.</summary>
    public static bool OpensAt(string text, int atIndex)
    {
        ArgumentNullException.ThrowIfNull(text);
        return (uint)atIndex < (uint)text.Length
            && text[atIndex] == '@'
            && (atIndex == 0 || !char.IsLetterOrDigit(text[atIndex - 1]));
    }

    /// <summary>The text typed between the "@" and the cursor; null when the list must close.</summary>
    public static string? QueryOf(TextAndSelection field, int atIndex)
    {
        ArgumentNullException.ThrowIfNull(field);
        var cursor = field.SelectionStart;
        if (!OpensAt(field.Text, atIndex) || cursor <= atIndex)
        {
            return null;
        }

        var typed = field.Text.AsSpan(atIndex + 1, cursor - atIndex - 1);
        return typed.Contains('\n') ? null : typed.ToString();
    }

    /// <summary>Replaces the "@" through the end of the selection, and the text typed after it, with <paramref name="mention"/>, and puts the cursor after it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The list would be closed: <see cref="QueryOf"/> would answer null.</exception>
    /// <exception cref="ArgumentException"><paramref name="mention"/> is empty.</exception>
    public static TextAndSelection Insert(TextAndSelection field, int atIndex, string mention)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentException.ThrowIfNullOrEmpty(mention);
        if (QueryOf(field, atIndex) is null)
        {
            throw new ArgumentOutOfRangeException(nameof(atIndex), "The index must be an \"@\" that opens the list, before the cursor.");
        }

        var text = string.Concat(field.Text.AsSpan(0, atIndex), mention, field.Text.AsSpan(field.SelectionStart + field.SelectionLength));
        return new TextAndSelection(text, atIndex + mention.Length, 0);
    }
}
