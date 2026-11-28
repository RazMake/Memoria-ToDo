namespace Memoria.ToDo.Shared.Editing;

/// <summary>Adds markers around the selection, or takes them off again.</summary>
internal static class Wrapping
{
    /// <summary>
    /// Wraps the selection in <paramref name="open"/> and <paramref name="close"/>,
    /// or removes them when the selection already sits between exactly those markers.
    /// </summary>
    public static TextAndSelection Toggle(TextAndSelection field, string open, string close)
    {
        var (text, start, length) = (field.Text, field.SelectionStart, field.SelectionLength);
        var end = start + length;

        if (text.AsSpan(0, start).EndsWith(open, StringComparison.Ordinal)
            && text.AsSpan(end).StartsWith(close, StringComparison.Ordinal))
        {
            return Unwrap(field, open.Length, close.Length);
        }

        return new TextAndSelection(
            string.Create(
                text.Length + open.Length + close.Length,
                (text, start, length, open, close),
                static (buffer, s) =>
                {
                    s.text.AsSpan(0, s.start).CopyTo(buffer);
                    var at = s.start;
                    s.open.CopyTo(buffer[at..]);
                    at += s.open.Length;
                    s.text.AsSpan(s.start, s.length).CopyTo(buffer[at..]);
                    at += s.length;
                    s.close.CopyTo(buffer[at..]);
                    at += s.close.Length;
                    s.text.AsSpan(s.start + s.length).CopyTo(buffer[at..]);
                }),
            start + open.Length,
            length);
    }

    /// <summary>Removes the <paramref name="openLength"/> characters before the selection and the <paramref name="closeLength"/> after it.</summary>
    public static TextAndSelection Unwrap(TextAndSelection field, int openLength, int closeLength)
    {
        var (text, start, length) = (field.Text, field.SelectionStart, field.SelectionLength);
        var end = start + length;
        return new TextAndSelection(
            string.Concat(text.AsSpan(0, start - openLength), text.AsSpan(start, length), text.AsSpan(end + closeLength)),
            start - openLength,
            length);
    }

    /// <summary>Replaces the <paramref name="oldOpenLength"/> characters before the selection with <paramref name="newOpen"/>.</summary>
    public static TextAndSelection ReplaceOpen(TextAndSelection field, int oldOpenLength, string newOpen)
    {
        var (text, start, length) = (field.Text, field.SelectionStart, field.SelectionLength);
        return new TextAndSelection(
            string.Concat(text.AsSpan(0, start - oldOpenLength), newOpen, text.AsSpan(start)),
            start - oldOpenLength + newOpen.Length,
            length);
    }
}
