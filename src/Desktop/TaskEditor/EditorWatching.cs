using ICSharpCode.AvalonEdit.Document;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>A place in a text field's text.</summary>
internal static class EditorWatching
{
    /// <summary>A place in the text that follows edits. Text typed at the place goes after it when <paramref name="staysBefore"/> is true, else before it.</summary>
    public static TextAnchor AnchorAt(this ITextField field, int offset, bool staysBefore)
    {
        var anchor = field.Document.CreateAnchor(offset);
        anchor.MovementType = staysBefore ? AnchorMovementType.BeforeInsertion : AnchorMovementType.AfterInsertion;
        return anchor;
    }
}
