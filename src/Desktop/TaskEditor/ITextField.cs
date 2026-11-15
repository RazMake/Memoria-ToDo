using ICSharpCode.AvalonEdit.Document;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The part of a text field that the editor's keys and pickers use: its text, selection, and cursor, and when they change.</summary>
internal interface ITextField
{
    TextDocument Document { get; }

    int SelectionStart { get; }

    int SelectionLength { get; }

    int CaretOffset { get; }

    /// <summary>Selects the range and puts the cursor at its end.</summary>
    void Select(int start, int length);

    /// <summary>Puts the keyboard cursor in the field.</summary>
    void FocusField();

    /// <summary>Raised after any edit and any move of the cursor.</summary>
    event Action Changed;

    /// <summary>Raised with the text the user typed.</summary>
    event Action<string> TextEntered;
}
