using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Puts a snippet into a text field as one undo step and leaves the cursor in the field.</summary>
internal static class SnippetInsertion
{
    /// <summary>
    /// Inserts the snippet. With <paramref name="replaceFrom"/> (an editor offset), the text from there to the end of the
    /// selection, such as a typed "/" and what followed, is replaced and is not the snippet's selected text. Without it,
    /// the selection is what the snippet works on.
    /// </summary>
    public static void Apply(StyledTextEditor field, ISnippet snippet, int? replaceFrom, SnippetInput input)
    {
        var from = replaceFrom is { } offset ? field.LfOffset(offset) : (int?)null;
        field.Run(text => snippet.Insert(from is { } start ? Without(text, start) : text, input));
        KeyboardFocus.MoveTo(field);
    }

    // The text with what lies from the start to the end of the selection removed, and the cursor at the start.
    private static TextAndSelection Without(TextAndSelection text, int start)
    {
        var end = text.SelectionStart + text.SelectionLength;
        return new TextAndSelection(text.Text.Remove(start, end - start), start, 0);
    }
}
