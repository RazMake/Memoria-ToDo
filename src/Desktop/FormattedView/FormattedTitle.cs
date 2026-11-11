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
    private const string LinkColorName = "blue";
    private static readonly FontFamily CodeFont = new("Consolas");

    private readonly IReadOnlyList<PaletteColor> _palette;
    private readonly ITheme _theme;
    private readonly LinkOpener _links;
    private string _markdown = "";
    private string? _shownMarkdown;
    private bool _shownDark;

    public FormattedTitle(IReadOnlyList<PaletteColor> palette, ITheme theme, IDefaultBrowser browser)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(browser);
        if (!palette.Any(color => color.Name == LinkColorName))
        {
            throw new ArgumentException($"The palette has no '{LinkColorName}' to color links with.", nameof(palette));
        }

        _palette = palette;
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
        var brushes = _palette.ToDictionary(
            color => color.Name,
            color => (Brush)Frozen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? color.Dark : color.Light))));
        var shownText = new System.Text.StringBuilder();
        foreach (var span in StyledSpans.Of(_markdown, MarkdownField.Title))
        {
            if (!span.Styles.HasFlag(SpanStyle.Marker))
            {
                Inlines.Add(InlineOf(span, brushes));
                shownText.Append(_markdown, span.Start, span.Length);
            }
        }

        AutomationProperties.SetName(this, shownText.ToString());
    }

    private Inline InlineOf(StyledSpan span, Dictionary<string, Brush> brushes)
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
        }

        if (span.Styles.HasFlag(SpanStyle.Strikethrough))
        {
            run.TextDecorations = System.Windows.TextDecorations.Strikethrough;
        }

        var colorName = span.Color ?? (span.LinkAddress is not null ? LinkColorName : null);
        if (colorName is not null)
        {
            run.Foreground = brushes[colorName];
        }

        if (span.LinkAddress is not { } address)
        {
            return run;
        }

        var link = new Hyperlink(run) { Foreground = run.Foreground };
        link.Click += (_, _) => _links.Click(address);
        return link;
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
