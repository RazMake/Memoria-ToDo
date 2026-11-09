using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Following a text editor's edits and cursor, and a place in its text.</summary>
internal static class EditorWatching
{
    /// <summary>Calls <paramref name="changed"/> after any edit and any move of the cursor.</summary>
    public static void WhenEditedOrMoved(this TextEditor editor, Action changed)
    {
        editor.TextChanged += (_, _) => changed();
        editor.TextArea.Caret.PositionChanged += (_, _) => changed();
    }

    /// <summary>A place in the text that follows edits. Text typed at the place goes after it when <paramref name="staysBefore"/> is true, else before it.</summary>
    public static TextAnchor AnchorAt(this TextEditor editor, int offset, bool staysBefore)
    {
        var anchor = editor.Document.CreateAnchor(offset);
        anchor.MovementType = staysBefore ? AnchorMovementType.BeforeInsertion : AnchorMovementType.AfterInsertion;
        return anchor;
    }
}
