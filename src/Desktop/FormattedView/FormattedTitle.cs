using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>A task's title as a reader sees it: native text, with no Markdown characters.</summary>
public sealed class FormattedTitle : TextBlock
{
    private static readonly FontFamily CodeFont = new(TextLooks.CodeFont);

    private readonly ITheme _theme;
    private readonly LinkOpener _links;
    private string _markdown = "";
    private string? _shownMarkdown;
    private bool _shownDark;

    public FormattedTitle(ITheme theme, IDefaultBrowser browser)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(browser);

        _theme = theme;
        _links = new LinkOpener(
            browser,
            address => OtherLinkClicked?.Invoke(this, address),
            failure => OpenFailed?.Invoke(this, failure));
        _ = new ThemeFollower(this, theme, Show);
    }

    /// <summary>
    /// The title's Markdown. Setting it shows it formatted, and sets the title's
    /// AutomationProperties.Name to the text shown.
    /// </summary>
    public string Markdown
    {
        get => _markdown;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _markdown = value;
            Show();
        }
    }

    /// <summary>Raised with the address of a clicked link that fails Rendering's browser check.</summary>
    public event EventHandler<string>? OtherLinkClicked;

    /// <summary>Raised when the default browser fails to open a web link.</summary>
    public event EventHandler<Exception>? OpenFailed;

    // Shows the Markdown in the current theme, unless that is what is already shown.
    private void Show()
    {
        var dark = _theme.IsDark;
        if (_shownMarkdown == _markdown && _shownDark == dark)
        {
            return;
        }

        _shownMarkdown = _markdown;
        _shownDark = dark;
        Inlines.Clear();
        var look = TextLooks.Of(dark);
        var spans = TitleText.ShownSpans(_markdown);
        foreach (var span in spans)
        {
            Inlines.Add(InlineOf(span, look));
        }

        AutomationProperties.SetName(this, TitleText.TextOf(_markdown, spans));
    }

    private Inline InlineOf(StyledSpan span, TextLook look)
    {
        var run = new Run(_markdown.Substring(span.Start, span.Length));
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

        if (span.Styles.HasFlag(SpanStyle.Mention))
        {
            // A mention is a chip, not a link: its clicks go to the element around the title.
            return new InlineUIContainer(new Chip(run, Frozen(new SolidColorBrush(ColorOf(look.MentionBackground)))));
        }

        if (span.LinkAddress is not { } address)
        {
            return run;
        }

        var link = new Hyperlink(run) { Foreground = run.Foreground };
        link.Click += (_, _) => _links.Click(address);
        // The click is the link's: its mouse-up doesn't reach the element around the title.
        link.MouseUp += (_, e) => e.Handled = true;
        link.MouseLeftButtonUp += (_, e) => e.Handled = true;
        return link;
    }

    private static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    // A mention's text on its background, with room around it, sitting on the title's baseline. The negative margin keeps the
    // room above and below from making the line taller.
    private sealed class Chip : Border
    {
        private readonly TextBlock _text;

        public Chip(Run run, Brush background)
        {
            _text = new TextBlock(run);
            Child = _text;
            Background = background;
            Padding = new Thickness(TextLooks.MentionPaddingSides, TextLooks.MentionPaddingTop, TextLooks.MentionPaddingSides, TextLooks.MentionPaddingBottom);
            Margin = new Thickness(0, -TextLooks.MentionPaddingTop, 0, -TextLooks.MentionPaddingBottom);
            CornerRadius = new CornerRadius(TextLooks.MentionCornerRadius);
        }

        // The title lines the chip up by this offset; without it the chip's bottom would sit on the baseline.
        protected override Size MeasureOverride(Size constraint)
        {
            var size = base.MeasureOverride(constraint);
            TextBlock.SetBaselineOffset(this, _text.FontFamily.Baseline * _text.FontSize);
            return size;
        }
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
