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
        var look = TextLooks.Of(dark);
        var spans = TitleText.ShownSpans(_markdown);
        foreach (var span in spans)
        {
            Inlines.Add(InlineOf(span, look));
        }

        AutomationProperties.SetName(this, TitleText.TextOf(_markdown, spans));
    }

    // The part of the title under the point: the mention there, boxed around all its lines, with the card MentionCard makes for it.
    internal Task<HoverPart?> PartAt(Point inTitle)
    {
        HoverPart? part = null;
        if (MentionCard is { } makeCard
            && ShownOffsetAt(inTitle) is { } offset
            && TitleMentions.At(_markdown, offset) is { } found
            && makeCard(found.Mention) is { } card)
        {
            part = new HoverPart(BoxOf(found.ShownStart, found.ShownLength), card);
        }

        return Task.FromResult(part);
    }

    // The offset into the shown text of the character under the point, or null when none is.
    // The position nearest the point sits between two characters, so the one under the point is the one after or before it.
    private int? ShownOffsetAt(Point point)
    {
        if (GetPositionFromPoint(point, snapToText: false) is not { } position)
        {
            return null;
        }

        var offset = new TextRange(ContentStart, position).Text.Length;
        if (position.GetNextInsertionPosition(LogicalDirection.Forward) is { } next
            && CharacterBox(position, next).Contains(point))
        {
            return offset;
        }

        return offset > 0
            && position.GetNextInsertionPosition(LogicalDirection.Backward) is { } previous
            && CharacterBox(previous, position).Contains(point)
            ? offset - 1
            : null;
    }

    // The one box around the characters of a shown range, which covers every line the range wraps onto.
    private Rect BoxOf(int shownStart, int shownLength)
    {
        var position = PositionAt(shownStart);
        var box = Rect.Empty;
        for (var i = 0; i < shownLength && position is not null; i++)
        {
            var next = position.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next is null)
            {
                break;
            }

            box.Union(CharacterBox(position, next));
            position = next;
        }

        return box;
    }

    // The box of the character between two neighbouring positions. WPF gives only a zero-width caret at a position,
    // so the box runs from the caret at the character's start to the caret at its end.
    // The end caret is the one that belongs to the character, which keeps a character at a line's end on its own line.
    private static Rect CharacterBox(TextPointer start, TextPointer end)
    {
        var box = start.GetCharacterRect(LogicalDirection.Forward);
        box.Union(end.GetCharacterRect(LogicalDirection.Backward));
        return box;
    }

    // The position before the character at an offset into the shown text.
    private TextPointer PositionAt(int shownOffset)
    {
        var position = ContentStart;
        var remaining = shownOffset;
        while (position.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.None)
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var length = position.GetTextRunLength(LogicalDirection.Forward);
                if (remaining < length)
                {
                    return position.GetPositionAtOffset(remaining)!;
                }

                remaining -= length;
            }

            position = position.GetNextContextPosition(LogicalDirection.Forward);
        }

        throw new InvalidOperationException("The offset is past the end of the shown text.");
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
            run.Background = Frozen(new SolidColorBrush(ColorOf(look.MentionBackground)));
            return run;
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

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
