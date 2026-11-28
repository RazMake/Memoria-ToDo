using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The mentions that show as chips in a document, in the document's own offsets: found again, once, after the text changes.
/// A mention whose text holds a line break stays Markdown, since a chip is one piece of one line.
/// </summary>
internal sealed class MentionsInText
{
    private readonly TextDocument _document;
    private readonly bool _isTitle;
    private IReadOnlyList<Mention>? _chips;

    public MentionsInText(TextDocument document, MarkdownField field)
    {
        _document = document;
        _isTitle = field == MarkdownField.Title;
        document.Changed += (_, _) => _chips = null;
    }

    /// <summary>The chips of the document's text now, in order.</summary>
    public IReadOnlyList<Mention> Chips => _chips ??= In(_document.Text);

    /// <summary>The mentions that show as chips in the text, in the text's own offsets; the text may use "\n" or "\r\n".</summary>
    public IReadOnlyList<Mention> In(string text) =>
        [.. Mentions.In(text, _isTitle).Where(mention => text.AsSpan(mention.Start, mention.Length).IndexOfAny('\r', '\n') < 0)];
}
