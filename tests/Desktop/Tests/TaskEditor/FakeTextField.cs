using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// A text field for unit tests: a real AvalonEdit document, so edits, anchors and undo behave as in the editor, with a
/// caret and selection that follow edits as the editor's do. Not a fake of behavior under test.
/// </summary>
internal sealed class FakeTextField : ITextField
{
    private int _caret;

    public FakeTextField(string text = "", int selectionStart = 0, int selectionLength = 0)
    {
        Document = new TextDocument(text);
        Document.Changed += OnDocumentChanged;
        Select(selectionStart, selectionLength);
        Document.UndoStack.ClearAll();
        FocusCount = 0;
    }

    public TextDocument Document { get; }

    public string Text => Document.Text;

    /// <summary>The text with "\n" for every line break, the way Editing sees it.</summary>
    public string LfText => Text.Replace("\r\n", "\n");

    public int SelectionStart { get; private set; }

    public int SelectionLength { get; private set; }

    public int CaretOffset => _caret;

    /// <summary>How many times the field was asked to take the keyboard focus.</summary>
    public int FocusCount { get; private set; }

    public event Action? Changed;

    public event Action<string>? TextEntered;

    /// <summary>Raised when the field loses the keyboard focus.</summary>
    public event Action? FocusLost;

    /// <summary>The field loses the keyboard focus, as when the user clicks elsewhere.</summary>
    public void LoseFocus() => FocusLost?.Invoke();

    public void Select(int start, int length)
    {
        (SelectionStart, SelectionLength, _caret) = (start, length, start + length);
        Changed?.Invoke();
    }

    public void FocusField() => FocusCount++;

    /// <summary>Puts the cursor at the offset with nothing selected, as a click does.</summary>
    public void Cursor(int offset) => Select(offset, 0);

    /// <summary>Types the text one character at a time over the selection, raising TextEntered for each.</summary>
    public void Type(string text)
    {
        foreach (var character in text)
        {
            var start = SelectionStart;
            Document.Replace(start, SelectionLength, character.ToString());
            Select(start + 1, 0);
            TextEntered?.Invoke(character.ToString());
        }
    }

    /// <summary>Deletes the character before the cursor.</summary>
    public void Backspace()
    {
        Document.Remove(_caret - 1, 1);
        Select(_caret, 0);
    }

    /// <summary>Deletes the character after the cursor, which stays where it is.</summary>
    public void Delete()
    {
        Document.Remove(_caret, 1);
        Select(_caret, 0);
    }

    /// <summary>Removes the range, as Ctrl+Backspace removes a word: the cursor lands where the range began.</summary>
    public void Erase(int start, int length)
    {
        Document.Remove(start, length);
        Select(_caret, 0);
    }

    /// <summary>Cuts the selection: removes it and leaves the cursor where it began.</summary>
    public void Cut() => Erase(SelectionStart, SelectionLength);

    public void Undo() => Document.UndoStack.Undo();

    /// <summary>Runs the command the way an editor key does, on this field.</summary>
    public bool Run(Func<TextAndSelection, TextAndSelection?> command) => TextEditorCommands.Run(this, command);

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        _caret = e.GetNewOffset(_caret, AnchorMovementType.AfterInsertion);
        (SelectionStart, SelectionLength) = (_caret, 0);
        Changed?.Invoke();
    }
}
