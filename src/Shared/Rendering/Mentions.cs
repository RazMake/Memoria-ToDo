using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Rendering;

/// <summary>A mention: where it sits in the Markdown, "[" through ")", the contact address, the template, and the stored text.</summary>
public sealed record Mention(int Start, int Length, string Address, string Template, string StoredText);

public static class Mentions
{
    private readonly record struct Found(Mention Mention, int LabelEnd);

    /// <summary>Every mention in the Markdown, in order.</summary>
    public static IReadOnlyList<Mention> In(string markdown, bool title)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Scan(markdown, title).ConvertAll(f => f.Mention);
    }

    /// <summary>The Markdown with each mention's text swapped for the callback's, escaped; null keeps the stored text.</summary>
    public static string WithTexts(string markdown, bool title, Func<Mention, string?> textOf)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(textOf);

        StringBuilder? result = null;
        var copied = 0;
        foreach (var found in Scan(markdown, title))
        {
            if (textOf(found.Mention) is not { } text)
            {
                continue;
            }

            var labelStart = found.Mention.Start + 1;
            result ??= new StringBuilder(markdown.Length + text.Length);
            result.Append(markdown, copied, labelStart - copied);
            AppendShownAsWritten(result, text);
            copied = found.LabelEnd;
        }

        return result?.Append(markdown, copied, markdown.Length - copied).ToString() ?? markdown;
    }

    /// <summary>A link is a mention when it is an inline link (its address is written beside its text, not in a reference definition) to a contact, names a template in its title, and its text holds no image.</summary>
    internal static bool IsMention(LinkInline link) =>
        !link.IsImage
        && link.Reference is null
        && !string.IsNullOrEmpty(link.Title)
        && link.Url is { } url
        && ContactAddress.IdOf(url) is not null
        && !link.Descendants<LinkInline>().Any(inner => inner.IsImage);

    private static List<Found> Scan(string markdown, bool title)
    {
        var found = new List<Found>();
        var document = Markdown.Parse(markdown, StyledSpans.PipelineOf(title ? MarkdownField.Title : MarkdownField.Description));
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsMention(link))
            {
                continue;
            }

            // The label ends at the "]" before the address, which only spaces, "(", or "<" can follow.
            var labelEnd = link.UrlSpan.Start - 1;
            while (markdown[labelEnd] != ']')
            {
                labelEnd--;
            }

            var start = link.Span.Start;
            var label = markdown.AsSpan(start + 1, labelEnd - start - 1);
            found.Add(new Found(
                new Mention(start, link.Span.End + 1 - start, link.Url!, link.Title!, Unescape(label)),
                labelEnd));
        }

        return found;
    }

    private static bool IsAsciiPunctuation(char c) => char.IsAscii(c) && (char.IsPunctuation(c) || char.IsSymbol(c));

    // A backslash before an ASCII punctuation character is dropped, as Markdown does.
    private static string Unescape(ReadOnlySpan<char> label)
    {
        if (!label.Contains('\\'))
        {
            return label.ToString();
        }

        var text = new StringBuilder(label.Length);
        for (var i = 0; i < label.Length; i++)
        {
            if (label[i] == '\\' && i + 1 < label.Length && IsAsciiPunctuation(label[i + 1]))
            {
                i++;
            }

            text.Append(label[i]);
        }

        return text.ToString();
    }

    // A backslash before an ASCII punctuation character always makes it plain text, so no Markdown character in the text,
    // such as "*", "]", ":red[", or the "." and "/" of an automatic link, does anything but show.
    private static void AppendShownAsWritten(StringBuilder to, string text)
    {
        foreach (var c in text)
        {
            if (IsAsciiPunctuation(c))
            {
                to.Append('\\');
            }

            to.Append(c);
        }
    }
}
