using System.Globalization;
using Markdig;
using Markdig.Syntax.Inlines;
using Memoria.ToDo.Shared.Rendering.ImageBox;

namespace Memoria.ToDo.Shared.Rendering;

public static class ImageWidths
{
    /// <summary>
    /// The Markdown with image number <paramref name="image"/>, as the description page numbers them, set to show
    /// <paramref name="width"/> pixels wide; a width already there is replaced.
    /// Throws ArgumentOutOfRangeException when the Markdown has no such image, or width is outside 1 to 10000.
    /// </summary>
    public static string With(string markdown, int image, int width)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, ImageBoxes.MinWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, ImageBoxes.MaxWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(image);

        var number = 0;
        foreach (var link in ImageBoxes.Images(Markdown.Parse(markdown, DescriptionMarkdown.Pipeline)))
        {
            if (number++ != image)
            {
                continue;
            }

            var label = ImageBoxes.LabelOf(markdown, link);
            var from = label.Bar >= 0 ? label.Bar : label.End + 1;
            var widthText = width.ToString(CultureInfo.InvariantCulture);
            if (link.Label is not null && link.LocalLabel != LocalLabel.Local)
            {
                // A shorthand or collapsed reference finds its definition by its alt text, which now ends in the width,
                // so it becomes an explicit reference: ![photo] and ![photo][] turn into ![photo|300][photo].
                // A full reference, ![alt][photo], already names its definition: its own label is replaced, not repeated.
                var after = markdown.AsSpan(label.End + 1);
                var rest = after.StartsWith("][]") ? label.End + 4
                    : after.StartsWith("][") ? link.LabelSpan.End + 2
                    : label.End + 2;
                return $"{markdown.AsSpan(0, from)}|{widthText}][{link.Label}]{markdown.AsSpan(rest)}";
            }

            return string.Concat(markdown.AsSpan(0, from), "|", widthText, markdown.AsSpan(label.End + 1));
        }

        throw new ArgumentOutOfRangeException(nameof(image), image, "The Markdown has no image with that number.");
    }
}
