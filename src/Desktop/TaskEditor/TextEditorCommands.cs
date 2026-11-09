using ICSharpCode.AvalonEdit;
using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Runs Editing's commands on a text editor's text and selection.</summary>
internal static class TextEditorCommands
{
    /// <summary>
    /// Runs the command on the editor's text and selection, with "\n" for every line break as Editing expects. The result
    /// replaces only the part of the text that changed, as one undo step, and becomes the selection. Returns false,
    /// changing nothing, when the command returns null because it doesn't apply.
    /// </summary>
    public static bool Run(this TextEditor editor, Func<TextAndSelection, TextAndSelection?> command)
    {
        var text = editor.Text;
        var result = command(Current(editor, text));
        if (result is null)
        {
            return false;
        }

        var changed = ChangedPart(text, result.Text);
        if (changed.OldLength > 0 || changed.New.Length > 0)
        {
            editor.Document.Replace(changed.Start, changed.OldLength, changed.New);
        }

        editor.Select(result.SelectionStart, result.SelectionLength);
        return true;
    }

    /// <summary>The editor's text and selection with "\n" for every line break, as Editing expects.</summary>
    public static TextAndSelection Current(this TextEditor editor) => Current(editor, editor.Text);

    /// <summary>The position in <see cref="Current(TextEditor)"/>'s text of the editor's character offset.</summary>
    public static int LfOffset(this TextEditor editor, int offset) => Lf(editor.Text[..offset]).Length;

    private static TextAndSelection Current(TextEditor editor, string text) =>
        text.Contains('\r')
            ? new TextAndSelection(
                Lf(text),
                Lf(text[..editor.SelectionStart]).Length,
                Lf(text.Substring(editor.SelectionStart, editor.SelectionLength)).Length)
            : new TextAndSelection(text, editor.SelectionStart, editor.SelectionLength);

    private static string Lf(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    // The characters between the text's common start and common end with the new text.
    private static (int Start, int OldLength, string New) ChangedPart(string old, string now)
    {
        var shortest = Math.Min(old.Length, now.Length);
        var start = 0;
        while (start < shortest && old[start] == now[start])
        {
            start++;
        }

        var end = 0;
        while (end < shortest - start && old[^(end + 1)] == now[^(end + 1)])
        {
            end++;
        }

        return (start, old.Length - start - end, now.Substring(start, now.Length - start - end));
    }
}
