using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Xunit;
using TitleControl = Memoria.ToDo.Desktop.FormattedView.FormattedTitle;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle;

/// <summary>
/// A title over the shared fakes, with every event it raises recorded. By default it sits in a showing window; pass
/// <c>inWindow: false</c> for a title that is laid out but never shown, which needs no desktop. Dispose it to close the window.
/// </summary>
internal sealed class TitleScreen : IDisposable
{
    /// <summary>A mention of Ana, shown as her first name.</summary>
    public const string AnaMention = "[Ana](contact:a1 \"{firstName}\")";

    /// <summary>A mention of Bo Li, shown as the full name.</summary>
    public const string BoMention = "[Bo Li](contact:b2 \"{fullName}\")";

    /// <summary>A mention of Ana Pop whose text holds escaped punctuation.</summary>
    public const string EscapedMention = "[Ana Pop \\(SDE\\)](contact:a1 \"{fullName} ({careerPath.short})\")";

    private readonly Window? window;
    private readonly Border around;
    private readonly double width;

    public TitleScreen(bool dark = false, double width = 480, bool inWindow = true)
    {
        this.width = width;
        Theme = new FakeTheme { IsDark = dark };
        Browser = new FakeDefaultBrowser();
        Title = new TitleControl(Theme, Browser, Pointer, Serilog.Core.Logger.None);
        Title.OtherLinkClicked += (_, address) => OtherLinks.Add(address);
        Title.OpenFailed += (_, failure) => OpenFailures.Add(failure);

        // Stands for a row around the title, which reacts to a click on it.
        around = new Border { Child = Title };
        around.MouseLeftButtonUp += (_, _) => ParentClicks++;
        around.MouseUp += (_, _) => ParentClicks++;
        if (inWindow)
        {
            window = new Window { Content = around, Width = width, Height = 160 };
            window.Show();
        }
    }

    public FakeTheme Theme { get; }

    public FakeDefaultBrowser Browser { get; }

    public FakeDragPointer Pointer { get; } = new();

    public TitleControl Title { get; }

    public List<string> OtherLinks { get; } = [];

    public List<Exception> OpenFailures { get; } = [];

    /// <summary>How many mouse-up events reached the element around the title.</summary>
    public int ParentClicks { get; private set; }

    public string Text => string.Concat(Runs().Select(run => run.Text));

    public static PaletteColor Red => Palette.Colors.Single(color => color.Name == "red");

    public static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static Border CardOf(string name) => new() { Width = 60, Height = 24, Tag = name };

    public static Point Middle(Rect box) => new(box.Left + (box.Width / 2), box.Top + (box.Height / 2));

    /// <summary>Whether the text is shown in the style: bold, italic, struck through or code.</summary>
    public static bool HasStyle(string style, TitleScreen screen, string text) => style switch
    {
        "bold" => screen.Run(text).FontWeight == FontWeights.Bold,
        "italic" => screen.Run(text).FontStyle == FontStyles.Italic,
        "struck through" => screen.IsStruckThrough(text),
        "code" => IsMonospaced(screen.Run(text).FontFamily),
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };

    /// <summary>Whether the text's own run, or the link around it, sets a color of its own.</summary>
    public bool HasOwnColor(string text) =>
        Run(text).ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue;

    /// <summary>
    /// Presses and releases the left mouse button on the text, as the routed events a click raises. Native hover and
    /// pressed state aren't modeled; for a link, the invoke that follows is what opens it.
    /// </summary>
    public void MouseClickOn(string text)
    {
        var run = Run(text);
        IInputElement target = (IInputElement?)FindLink(run) ?? Title;
        foreach (var routed in new[] { Mouse.PreviewMouseDownEvent, Mouse.MouseDownEvent, Mouse.PreviewMouseUpEvent, Mouse.MouseUpEvent })
        {
            target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = routed,
                Source = target,
            });
        }

        if (target is Hyperlink link)
        {
            HyperlinkActivation.Invoke(link);
        }
    }

    /// <summary>Whether the text is shown inside a link.</summary>
    public bool IsInLink(string text) => FindLink(Run(text)) is not null;

    /// <summary>
    /// A point in the title's coordinates just inside the start of the first run that holds the text, halfway down its line.
    /// For text in the title itself, not a mention's chip; use <see cref="ChipBox"/> for a mention.
    /// </summary>
    public Point InsideStartOf(string text)
    {
        Layout();
        var rect = Run(text).ContentStart.GetCharacterRect(LogicalDirection.Forward);
        return new Point(rect.X + 2, rect.Y + rect.Height / 2);
    }

    /// <summary>The chip that shows the mention with the text: its whole background, padding included, in the title's coordinates.</summary>
    public Rect ChipBox(string text)
    {
        Layout();
        return Title.ChipBox(text);
    }

    /// <summary>Rests the fake pointer at the title's point.</summary>
    public void RestOn(Point inTitle)
    {
        SimulatedPointer.RequireActiveWindow(Title);
        SimulatedPointer.Move(Title, Pointer, inTitle);
    }

    /// <summary>Lets the dispatcher work until the card is on screen, or ten seconds pass; says whether it showed.</summary>
    public bool WaitForShown(FrameworkElement card)
    {
        if (card.IsLoaded)
        {
            return true;
        }

        var frame = new DispatcherFrame();
        RoutedEventHandler loaded = (_, _) => frame.Continue = false;
        var timeout = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Normal, (_, _) => frame.Continue = false, Title.Dispatcher);
        card.Loaded += loaded;
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timeout.Stop();
            card.Loaded -= loaded;
        }

        return card.IsLoaded;
    }

    public Run Run(string text) => Runs().First(run => run.Text.Contains(text));

    public Color ColorOf(string text) => Assert.IsType<SolidColorBrush>(Run(text).Foreground).Color;

    public bool IsStruckThrough(string text)
    {
        for (TextElement? element = Run(text); element is not null; element = element.Parent as TextElement)
        {
            if (element is Inline { TextDecorations: { } decorations }
                && decorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Closes the window and returns once the title has raised Unloaded, so it is off screen as far as WPF is concerned.</summary>
    public void Close() => LoadSync.Close(window!, Title);

    public void Dispose() => window?.Close();

    /// <summary>The chip a mention shows as: the border around the mention's text.</summary>
    public Border Chip(string text) => Title.Chip(text);

    private void Layout()
    {
        if (window is not null)
        {
            window.UpdateLayout();
            return;
        }

        around.Measure(new Size(width, 160));
        around.Arrange(new Rect(0, 0, width, 160));
    }

    private static Hyperlink? FindLink(TextElement run)
    {
        DependencyObject? element = run;
        while (element is not null and not Hyperlink)
        {
            element = (element as TextElement)?.Parent;
        }

        return element as Hyperlink;
    }

    private static bool IsMonospaced(FontFamily family)
    {
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyphs))
        {
            return false;
        }

        return glyphs.AdvanceWidths[glyphs.CharacterToGlyphMap['i']] == glyphs.AdvanceWidths[glyphs.CharacterToGlyphMap['W']];
    }

    private IEnumerable<Run> Runs() => TitleChips.Walk(Title.Inlines).OfType<Run>();
}
