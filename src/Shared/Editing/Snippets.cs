using System.Globalization;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Editing;

/// <summary>The built-in snippets, in the order the picker lists them.</summary>
public static class Snippets
{
    public static ISnippet DateStamp { get; } = new Snippet(
        "Date stamp", SnippetNeeds.Time, SnippetPlacement.Inline,
        (_, input) => new(input.Now!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        worksInTitle: true);

    public static ISnippet DateAndTimeStamp { get; } = new Snippet(
        "Date and time stamp", SnippetNeeds.Time, SnippetPlacement.Inline,
        (_, input) => new(input.Now!.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
        worksInTitle: true);

    public static ISnippet Note { get; } = Alert("Note", "NOTE");

    public static ISnippet Tip { get; } = Alert("Tip", "TIP");

    public static ISnippet Warning { get; } = Alert("Warning", "WARNING");

    public static ISnippet Link { get; } = new Snippet(
        "Link", SnippetNeeds.ClipboardText, SnippetPlacement.Inline,
        (selected, input) => BuildLink(selected, input.ClipboardText!),
        worksInTitle: true);

    public static ISnippet Image { get; } = new Snippet(
        "Image", SnippetNeeds.ImageMarkdown, SnippetPlacement.Inline,
        (_, input) => new(input.ImageMarkdown!));

    public static ISnippet CodeBlock { get; } = new Snippet(
        "Code block", SnippetNeeds.None, SnippetPlacement.Block,
        (_, _) => new("```\n\n```", "```\n".Length));

    public static ISnippet TaskReference { get; } = new Snippet(
        "Task reference", SnippetNeeds.TaskToReference, SnippetPlacement.Inline,
        (_, input) => new($"[{MarkdownLabel.Escape(input.Task!.Title)}]({input.Task.Address})"),
        worksInTitle: true);

    public static ISnippet Table { get; } = new Snippet(
        "Table", SnippetNeeds.None, SnippetPlacement.BlockAfterBlankLine,
        (_, _) => new("| Column 1 | Column 2 |\n| --- | --- |\n|  |  |"));

    public static ISnippet HorizontalLine { get; } = new Snippet(
        "Horizontal line", SnippetNeeds.None, SnippetPlacement.BlockAfterBlankLine,
        (_, _) => new("---"));

    public static IReadOnlyList<ISnippet> All { get; } =
    [
        DateStamp, DateAndTimeStamp, Note, Tip, Warning, Link, Image, CodeBlock, TaskReference, Table, HorizontalLine,
    ];

    private static Snippet Alert(string name, string kind) => new(
        name, SnippetNeeds.None, SnippetPlacement.Block,
        (_, _) => new($"> [!{kind}]\n> "));

    /// <summary>[text](address) with the clipboard's URL as the address, or the cursor in an empty address.</summary>
    private static SnippetContent BuildLink(ReadOnlySpan<char> selected, string clipboardText)
    {
        var url = clipboardText.Trim();
        var text = selected.ToString();
        if (!IsWebAddress(url))
        {
            return new($"[{text}]()", text.Length + "[](".Length);
        }

        var link = $"[{text}]({url})";
        return text.Length == 0 ? new(link, 1) : new(link);
    }

    private static bool IsWebAddress(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }
}
