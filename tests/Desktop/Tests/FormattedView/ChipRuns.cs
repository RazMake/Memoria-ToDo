using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

/// <summary>How tests read the text a mention chip shows: the runs inside it, wherever the chip sits.</summary>
internal static class ChipRuns
{
    /// <summary>Every inline of <paramref name="inlines"/>, going into spans and into the text of each mention chip.</summary>
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
            else if (inline is InlineUIContainer { Child: MentionChip chip })
            {
                foreach (var inner in Walk(TextOf(chip).Inlines))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>The runs inside a chip's text, in order.</summary>
    public static IEnumerable<Run> RunsOf(Border chip) => Walk(TextOf(chip).Inlines).OfType<Run>();

    /// <summary>The text a chip shows: all its runs together.</summary>
    public static string ShownBy(Border chip) => string.Concat(RunsOf(chip).Select(run => run.Text));

    /// <summary>The first run of the chip whose text holds <paramref name="text"/>.</summary>
    public static Run Run(Border chip, string text) => RunsOf(chip).First(run => run.Text.Contains(text));

    // The chip's text element, found in its logical tree, so the tests don't depend on how the chip nests it.
    private static TextBlock TextOf(Border chip) =>
        Find(chip) ?? throw new InvalidOperationException("The chip holds no text.");

    private static TextBlock? Find(DependencyObject element)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            if (child is TextBlock text)
            {
                return text;
            }

            if (Find(child) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }
}
