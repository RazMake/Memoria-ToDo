namespace Memoria.ToDo.Shared.Editing.Tests;

/// <summary>
/// Writes a field as text with its selection marked, so a test reads as before and after:
/// <c>«</c> and <c>»</c> surround the selected text, and an empty <c>«»</c> is a cursor.
/// </summary>
internal static class Marked
{
    public static TextAndSelection Field(string marked)
    {
        var start = marked.IndexOf('«');
        var end = marked.IndexOf('»');
        if (start < 0 || end < start)
        {
            throw new ArgumentException($"Mark the selection with « and »: {marked}", nameof(marked));
        }

        var text = marked.Remove(end, 1).Remove(start, 1);
        return new TextAndSelection(text, start, end - 1 - start);
    }

    public static string Show(TextAndSelection field) =>
        field.Text
            .Insert(field.SelectionStart + field.SelectionLength, "»")
            .Insert(field.SelectionStart, "«");
}
