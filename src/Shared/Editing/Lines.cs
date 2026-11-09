namespace Memoria.ToDo.Shared.Editing;

/// <summary>Finds the line around an index in text whose lines end with "\n".</summary>
internal static class Lines
{
    /// <summary>The index of the first character of the line that holds <paramref name="index"/>.</summary>
    public static int StartOf(string text, int index) => text.AsSpan(0, index).LastIndexOf('\n') + 1;

    /// <summary>The index just past the last character of the line that holds <paramref name="index"/>, before its "\n".</summary>
    public static int EndOf(string text, int index)
    {
        var newline = text.AsSpan(index).IndexOf('\n');
        return newline < 0 ? text.Length : index + newline;
    }
}
