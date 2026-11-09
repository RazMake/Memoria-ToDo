using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Gives each stretch of a text editor's text the look its <see cref="StyledSpan"/> asks for, leaving the characters as they
/// are. Spans may be older than the text for a moment, so a span is cut to the line it lands on.
/// </summary>
internal sealed class MarkdownStyler : DocumentColorizingTransformer
{
    private const string LinkColorName = "blue";
    private const double MarkerOpacity = 0.5;

    // Level 1 to 6; index 0 is for text that is no heading.
    private static readonly double[] HeadingScale = [1, 1.6, 1.4, 1.25, 1.15, 1.1, 1.05];
    private static readonly FontFamily CodeFont = new("Consolas");
    private static readonly Brush CodeBackground = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0x80, 0x80, 0x80)));

    private readonly IReadOnlyList<PaletteColor> _palette;
    private readonly ITheme _theme;
    private IReadOnlyList<StyledSpan> _spans = [];
    private Dictionary<string, Brush> _colors = [];

    public MarkdownStyler(IReadOnlyList<PaletteColor> palette, ITheme theme)
    {
        _palette = palette;
        _theme = theme;
        if (!palette.Any(color => color.Name == LinkColorName))
        {
            throw new ArgumentException($"The palette has no '{LinkColorName}' to color links with.", nameof(palette));
        }

        UseThemeColors();
    }

    /// <summary>Styles the text with the spans, and takes the colors of the current theme.</summary>
    public void Update(IReadOnlyList<StyledSpan> spans)
    {
        _spans = spans;
        UseThemeColors();
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (var span in _spans)
        {
            if (span.Start >= line.EndOffset)
            {
                break;
            }

            var start = Math.Max(span.Start, line.Offset);
            var end = Math.Min(span.Start + span.Length, line.EndOffset);
            if (start < end)
            {
                ChangeLinePart(start, end, element => Apply(span, element.TextRunProperties));
            }
        }
    }

    private void UseThemeColors() =>
        _colors = _palette.ToDictionary(
            color => color.Name,
            color => (Brush)Frozen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(_theme.IsDark ? color.Dark : color.Light))));

    private void Apply(StyledSpan span, VisualLineElementTextRunProperties properties)
    {
        var styles = span.Styles;
        var typeface = properties.Typeface;
        var bold = styles.HasFlag(SpanStyle.Bold) || span.HeadingLevel > 0;
        properties.SetTypeface(new Typeface(
            styles.HasFlag(SpanStyle.Code) ? CodeFont : typeface.FontFamily,
            styles.HasFlag(SpanStyle.Italic) ? FontStyles.Italic : typeface.Style,
            bold ? FontWeights.Bold : typeface.Weight,
            typeface.Stretch));
        properties.SetFontRenderingEmSize(properties.FontRenderingEmSize * HeadingScale[span.HeadingLevel]);

        if (styles.HasFlag(SpanStyle.Code))
        {
            properties.SetBackgroundBrush(CodeBackground);
        }

        var decorations = new TextDecorationCollection();
        if (styles.HasFlag(SpanStyle.Strikethrough))
        {
            decorations.Add(TextDecorations.Strikethrough);
        }

        if (span.LinkAddress is not null)
        {
            decorations.Add(TextDecorations.Underline);
        }

        if (decorations.Count > 0)
        {
            properties.SetTextDecorations(decorations);
        }

        var brush = ColorOf(span) ?? properties.ForegroundBrush;
        properties.SetForegroundBrush(styles.HasFlag(SpanStyle.Marker) ? Dimmed(brush) : brush);
    }

    // A color name the palette lacks never reaches a span, so a missing one is a bug and throws.
    private Brush? ColorOf(StyledSpan span) =>
        span.Color is not null ? _colors[span.Color]
        : span.LinkAddress is not null ? _colors[LinkColorName]
        : null;

    private static Brush Dimmed(Brush brush)
    {
        var dimmed = brush.Clone();
        dimmed.Opacity = brush.Opacity * MarkerOpacity;
        return Frozen(dimmed);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
