namespace Memoria.ToDo.Shared.Editing;

/// <summary>The commands that wrap the selection in Markdown, or remove it again.</summary>
public static class Formatting
{
    public static TextAndSelection Bold(TextAndSelection field) => throw new NotImplementedException();

    public static TextAndSelection Italic(TextAndSelection field) => throw new NotImplementedException();

    public static TextAndSelection Strikethrough(TextAndSelection field) => throw new NotImplementedException();

    public static TextAndSelection InlineCode(TextAndSelection field) => throw new NotImplementedException();

    /// <exception cref="ArgumentException"><paramref name="color"/> is not in <paramref name="paletteNames"/>.</exception>
    public static TextAndSelection Color(TextAndSelection field, string color, IReadOnlyCollection<string> paletteNames) => throw new NotImplementedException();

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not 1, 2, or 3.</exception>
    public static TextAndSelection Heading(TextAndSelection field, int level) => throw new NotImplementedException();
}
