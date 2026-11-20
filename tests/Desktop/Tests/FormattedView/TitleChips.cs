using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

/// <summary>How tests find a mention's chip in a shown title. A mention is a chip inside the title, with its own text.</summary>
internal static class TitleChips
{
    /// <summary>Every inline of the title, going into spans and into the text of each chip.</summary>
    public static IEnumerable<Inline> Walk(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            yield return inline;
            if (inline is Span span)
            {
                foreach (var inner in Walk(span.Inlines))
                {
                    yield return inner;
                }
            }
            else if (inline is InlineUIContainer { Child: Border { Child: TextBlock chipText } })
            {
                foreach (var inner in Walk(chipText.Inlines))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>The chip a mention shows as: the border around the mention's text, found by the text of all its runs together.</summary>
    public static Border Chip(this FormattedTitle title, string text) =>
        title.Chips().First(chip => TextOf(chip).Contains(text));

    /// <summary>Every chip the title shows, in order. One mention is one chip.</summary>
    public static Border[] Chips(this FormattedTitle title) =>
        [.. Walk(title.Inlines).OfType<InlineUIContainer>().Select(container => container.Child).OfType<Border>().Where(border => border.Child is TextBlock)];

    /// <summary>The text a chip shows: all its runs together.</summary>
    public static string TextOf(Border chip) => string.Concat(RunsOf(chip).Select(run => run.Text));

    /// <summary>The runs inside a chip's text, in order.</summary>
    public static IEnumerable<Run> RunsOf(Border chip) => Walk(((TextBlock)chip.Child).Inlines).OfType<Run>();

    /// <summary>The chip's whole background, padding included, in the title's coordinates. The title must be laid out.</summary>
    public static Rect ChipBox(this FormattedTitle title, string text)
    {
        var chip = title.Chip(text);
        return chip.TransformToAncestor(title).TransformBounds(new Rect(chip.RenderSize));
    }
}
