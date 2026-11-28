using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>Turns a styled span of Markdown into the WPF run that shows it, for titles and mention chips alike.</summary>
internal static class StyledRun
{
    private static readonly FontFamily CodeFont = new(TextLooks.CodeFont);

    /// <summary>The text of <paramref name="span"/>, taken from <paramref name="markdown"/>, with the span's own styles.</summary>
    public static Run Of(string markdown, StyledSpan span, TextLook look)
    {
        var run = new Run(markdown.Substring(span.Start, span.Length));
        if (span.Styles.HasFlag(SpanStyle.Bold))
        {
            run.FontWeight = FontWeights.Bold;
        }

        if (span.Styles.HasFlag(SpanStyle.Italic))
        {
            run.FontStyle = FontStyles.Italic;
        }

        if (span.Styles.HasFlag(SpanStyle.Code))
        {
            run.FontFamily = CodeFont;
            run.Background = Frozen(new SolidColorBrush(ColorOf(look.CodeBackground)) { Opacity = look.CodeBackgroundOpacity });
        }

        if (span.Styles.HasFlag(SpanStyle.Strikethrough))
        {
            run.TextDecorations = System.Windows.TextDecorations.Strikethrough;
        }

        if (look.ColorOf(span) is { } color)
        {
            run.Foreground = Frozen(new SolidColorBrush(ColorOf(color)));
        }

        return run;
    }

    /// <summary>A frozen brush of the "#RRGGBB" color <paramref name="hex"/>.</summary>
    public static SolidColorBrush Brush(string hex) => Frozen(new SolidColorBrush(ColorOf(hex)));

    private static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
