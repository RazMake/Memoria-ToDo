namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The rule for a one-line field that text with line breaks is pasted into.</summary>
internal static class OneLine
{
    /// <summary>The text's lines, each trimmed, joined with one space and trimmed as a whole.</summary>
    public static string Of(string text) =>
        string.Join(' ', text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None).Select(line => line.Trim())).Trim();
}
