using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Memoria.ToDo.Shared.Rendering.ImageBox;

/// <summary>Where a width sits in an image's alt text: <see cref="End"/> is the last character of the alt text, <see cref="Bar"/> the <c>|</c> before the width or -1 when it has none.</summary>
internal readonly record struct ImageLabel(int End, int Bar, int Width);

/// <summary>
/// The one home of how an image is numbered and how its width is read, so the page and <see cref="ImageWidths"/> agree.
/// Images are numbered from 0 in the order the Markdown holds them, including one the page shows as a link.
/// A width is the digits after the last <c>|</c> of the alt text, from 1 to 10000; anything else stays alt text.
/// Every position is an index into the Markdown the document was parsed from, with precise source locations on.
/// </summary>
internal static class ImageBoxes
{
    public const int MinWidth = 1;
    public const int MaxWidth = 10000;
    private const int MaxDigits = 5;

    public static IEnumerable<LinkInline> Images(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.IsImage)
            {
                yield return link;
            }
        }
    }

    public static ImageLabel LabelOf(string markdown, LinkInline image)
    {
        var start = image.Span.Start + 2;
        var end = image.LastChild is { } last ? last.Span.End : start - 1;
        var bar = end;
        while (bar >= start && markdown[bar] is >= '0' and <= '9')
        {
            bar--;
        }

        var digits = end - bar;
        if (digits is >= 1 and <= MaxDigits
            && bar >= start
            && markdown[bar] == '|'
            && (bar == start || markdown[bar - 1] != '\\')
            && int.Parse(markdown.AsSpan(bar + 1, digits)) is var width and >= MinWidth and <= MaxWidth)
        {
            return new ImageLabel(end, bar, width);
        }

        return new ImageLabel(end, -1, 0);
    }

    /// <summary>Wraps each image in an <see cref="ImageBoxInline"/>, with its width and number, and takes the width out of its alt text.</summary>
    public static void Box(MarkdownDocument document, string markdown)
    {
        var number = 0;
        foreach (var image in Images(document).ToList())
        {
            var label = LabelOf(markdown, image);
            if (label.Bar >= 0)
            {
                CutFrom(image, label.Bar);
            }

            var box = new ImageBoxInline(number++, label.Width);
            image.ReplaceBy(box, copyChildren: false);
            box.AppendChild(image);
        }
    }

    // Drops the alt text from the source index on.
    private static void CutFrom(LinkInline image, int index)
    {
        while (image.LastChild is { } last && last.Span.Start >= index)
        {
            last.Remove();
        }

        if (image.LastChild is LiteralInline literal
            && literal.Span.End >= index
            && literal.Span.Length == literal.Content.Length)
        {
            var content = literal.Content;
            literal.Content = new StringSlice(content.Text, content.Start, content.Start + (index - literal.Span.Start) - 1);
        }
    }
}
