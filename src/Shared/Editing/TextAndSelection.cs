namespace Memoria.ToDo.Shared.Editing;

/// <summary>A field's text and its selection: the input and the result of every editing command.</summary>
public sealed record TextAndSelection
{
    /// <exception cref="ArgumentOutOfRangeException">The selection is not inside the text.</exception>
    public TextAndSelection(string text, int selectionStart, int selectionLength) => throw new NotImplementedException();

    public string Text => throw new NotImplementedException();

    public int SelectionStart => throw new NotImplementedException();

    public int SelectionLength => throw new NotImplementedException();
}
