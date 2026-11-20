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
    private readonly List<Chip> _chips = [];
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
        _ = new HoverCard(this, PartAt);
    }

    /// <summary>
    /// Makes the card shown by a mention while the pointer rests on it, from the mention's address and template.
    /// Returns null to show none. Unset, no mention shows a card.
    /// </summary>
    public Func<Mention, FrameworkElement?>? MentionCard { get; set; }

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
        _chips.Clear();
        var look = TextLooks.Of(dark);
        var spans = TitleText.ShownSpans(_markdown);
        var mentions = Mentions.In(_markdown, title: true);
        var background = Frozen(new SolidColorBrush(ColorOf(look.MentionBackground)));
        for (var i = 0; i < spans.Count; i++)
        {
            if (!spans[i].Styles.HasFlag(SpanStyle.Mention))
            {
                Inlines.Add(InlineOf(spans[i], look));
                continue;
            }

            // A mention is one chip, however many styled spans its text has. A chip is not a link: its clicks go to the
            // element around the title.
            var mention = mentions.First(m => spans[i].Start >= m.Start && spans[i].Start < m.Start + m.Length);
            var runs = new List<Run> { RunOf(spans[i], look) };
            while (i + 1 < spans.Count
                && spans[i + 1].Styles.HasFlag(SpanStyle.Mention)
                && spans[i + 1].Start < mention.Start + mention.Length)
            {
                i++;
                runs.Add(RunOf(spans[i], look));
            }

            var chip = new Chip(mention, runs, background);
            _chips.Add(chip);
            Inlines.Add(new InlineUIContainer(chip));
        }

        AutomationProperties.SetName(this, TitleText.TextOf(_markdown, spans));
    }

    // The part of the title under the point: the mention whose chip is there, boxed by that chip, with the card MentionCard
    // makes for it. A chip's text isn't in the title's own text flow, so the chips are hit-tested directly, by their boxes
    // in the title.
    internal Task<HoverPart?> PartAt(Point inTitle)
    {
        HoverPart? part = null;
        if (MentionCard is { } makeCard
            && _chips.FirstOrDefault(chip => BoxOf(chip).Contains(inTitle)) is { } hit
            && makeCard(hit.Mention) is { } card)
        {
            part = new HoverPart(BoxOf(hit), card);
        }

        return Task.FromResult(part);
    }

    // The chip's whole background, padding included, in the title's coordinates. Empty while the chip isn't laid out in the title.
    private Rect BoxOf(Chip chip) =>
        chip.IsDescendantOf(this)
            ? chip.TransformToAncestor(this).TransformBounds(new Rect(chip.RenderSize))
            : Rect.Empty;

    // The span's text with the span's own styles.
    private Run RunOf(StyledSpan span, TextLook look)
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

        return run;
    }

    // A span outside any mention: its run, or a link around it.
    private Inline InlineOf(StyledSpan span, TextLook look)
    {
        var run = RunOf(span, look);
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

        public Chip(Mention mention, IEnumerable<Run> runs, Brush background)
        {
            Mention = mention;
            _text = new TextBlock();
            _text.Inlines.AddRange(runs);
            Child = _text;
            Background = background;
            Padding = new Thickness(TextLooks.MentionPaddingSides, TextLooks.MentionPaddingTop, TextLooks.MentionPaddingSides, TextLooks.MentionPaddingBottom);
            Margin = new Thickness(0, -TextLooks.MentionPaddingTop, 0, -TextLooks.MentionPaddingBottom);
            CornerRadius = new CornerRadius(TextLooks.MentionCornerRadius);
        }

        public Mention Mention { get; }

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
