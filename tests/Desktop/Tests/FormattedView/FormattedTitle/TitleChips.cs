using System.Windows;
using System.Windows.Documents;
using TitleControl = Memoria.ToDo.Desktop.FormattedView.FormattedTitle;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle;

/// <summary>How tests find a mention's chip in a shown title. A mention is a chip inside the title, with its own text.</summary>
internal static class TitleChips
{
    /// <summary>The chip a mention shows as, found by the text of all its runs together.</summary>
    public static MentionChip Chip(this TitleControl title, string text) =>
        title.Chips().First(chip => ChipRuns.ShownBy(chip).Contains(text));

    /// <summary>Every chip the title shows, in order. One mention is one chip.</summary>
    public static MentionChip[] Chips(this TitleControl title) =>
        [.. ChipRuns.Walk(title.Inlines).OfType<InlineUIContainer>().Select(container => container.Child).OfType<MentionChip>()];

    /// <summary>The chip's whole background, padding included, in the title's coordinates. The title must be laid out.</summary>
    public static Rect ChipBox(this TitleControl title, string text)
    {
        var chip = title.Chip(text);
        return chip.TransformToAncestor(title).TransformBounds(new Rect(chip.RenderSize));
    }
}
