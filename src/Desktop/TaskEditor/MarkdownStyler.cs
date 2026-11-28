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
    private const double MarkerOpacity = 0.5;

    private static readonly FontFamily CodeFont = new(TextLooks.CodeFont);

    // The code background of the light look, then of the dark one.
    private static readonly Brush[] CodeBackgrounds = [CodeBackgroundOf(TextLooks.Light), CodeBackgroundOf(TextLooks.Dark)];

    private readonly ITheme _theme;
    private readonly Dictionary<Brush, Brush> _dimmed = [];
    private IReadOnlyList<StyledSpan> _spans = [];
    private TextLook _look = TextLooks.Light;
    private Brush _codeBackground = CodeBackgrounds[0];

    public MarkdownStyler(ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _theme = theme;
        UseThemeLook();
    }

    /// <summary>Styles the text with the spans, in the look of the current theme.</summary>
    public void Update(IReadOnlyList<StyledSpan> spans)
    {
        _spans = spans;
        UseThemeLook();
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
            if (start < end && !span.Styles.HasFlag(SpanStyle.Mention)) // A chip draws itself.
            {
                ChangeLinePart(start, end, element => Apply(span, element.TextRunProperties));
            }
        }
    }

    private void UseThemeLook()
    {
        var dark = _theme.IsDark;
        var look = TextLooks.Of(dark);
        if (look == _look)
        {
            return;
        }

        _look = look;
        _codeBackground = CodeBackgrounds[dark ? 1 : 0];
        _dimmed.Clear();
    }

    private static Brush CodeBackgroundOf(TextLook look)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(look.CodeBackground)) { Opacity = look.CodeBackgroundOpacity };
        brush.Freeze();
        return brush;
    }

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
        properties.SetFontRenderingEmSize(properties.FontRenderingEmSize * TextLooks.HeadingScale(span.HeadingLevel));

        if (styles.HasFlag(SpanStyle.Code))
        {
            properties.SetBackgroundBrush(_codeBackground);
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

        var brush = _look.ColorOf(span) is { } color ? TagLook.Paint(color) : properties.ForegroundBrush;
        properties.SetForegroundBrush(styles.HasFlag(SpanStyle.Marker) ? Dimmed(brush) : brush);
    }

    private Brush Dimmed(Brush brush)
    {
        if (!_dimmed.TryGetValue(brush, out var dimmed))
        {
            dimmed = brush.Clone();
            dimmed.Opacity = brush.Opacity * MarkerOpacity;
            dimmed.Freeze();
            _dimmed.Add(brush, dimmed);
        }

        return dimmed;
    }
}
