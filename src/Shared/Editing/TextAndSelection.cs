namespace Memoria.ToDo.Shared.Editing;

/// <summary>A field's text and its selection: the input and the result of every editing command.</summary>
public sealed record TextAndSelection
{
    /// <exception cref="ArgumentOutOfRangeException">The selection is not inside the text.</exception>
    public TextAndSelection(string text, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionStart);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionLength);
        if (selectionStart > text.Length || selectionLength > text.Length - selectionStart)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionLength), "The selection must end inside the text.");
        }

        Text = text;
        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
    }

    public string Text { get; }

    public int SelectionStart { get; }

    public int SelectionLength { get; }
}
