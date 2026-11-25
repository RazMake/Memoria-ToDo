using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>
/// A mention's text as one chip, as a reader sees it: its Markdown formatted as a title's is, with no Markdown
/// characters, in the normal text color, on the theme's mention background, with the mention padding and rounded
/// corners. The room above and below the text doesn't make a line taller, and the chip sits on the text's baseline.
/// It takes its font from where it sits, and doesn't follow the theme: its user makes a new chip when the theme changes.
/// Its AutomationProperties.Name is the text it shows.
/// </summary>
public sealed class MentionChip : Border
{
    private readonly TextBlock _text = new();

    /// <param name="text">The mention's text as a mention link's text holds it, such as "Ana Pop (_anap_)".</param>
    /// <param name="dark">Whether to use the dark theme's look.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public MentionChip(string text, bool dark)
    {
        ArgumentNullException.ThrowIfNull(text);

        var look = TextLooks.Of(dark);
        var spans = TitleText.ShownSpans(text);
        foreach (var span in spans)
        {
            // A mention's text keeps the normal text color, whatever its styles, as Rendering's look does for a mention's spans.
            _text.Inlines.Add(StyledRun.Of(text, span with { Styles = span.Styles | SpanStyle.Mention }, look));
        }

        Child = _text;
        Background = StyledRun.Brush(look.MentionBackground);
        Padding = new Thickness(TextLooks.MentionPaddingSides, TextLooks.MentionPaddingTop, TextLooks.MentionPaddingSides, TextLooks.MentionPaddingBottom);
        Margin = new Thickness(0, -TextLooks.MentionPaddingTop, 0, -TextLooks.MentionPaddingBottom);
        CornerRadius = new CornerRadius(TextLooks.MentionCornerRadius);
        AutomationProperties.SetName(this, TitleText.TextOf(text, spans));
    }

    // The line lines the chip up by this offset; without it the chip's bottom would sit on the baseline.
    protected override Size MeasureOverride(Size constraint)
    {
        var size = base.MeasureOverride(constraint);
        TextBlock.SetBaselineOffset(this, _text.FontFamily.Baseline * _text.FontSize);
        return size;
    }
}
