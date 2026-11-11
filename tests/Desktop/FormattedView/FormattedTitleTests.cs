using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

[UX]
public class FormattedTitleTests
{
    [WpfFact]
    public void Shows_the_title_as_text_with_no_Markdown_characters()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = "Buy **fresh** *whole* ~~old~~ `milk` :red[today]";

        Assert.Equal("Buy fresh whole old milk today", screen.Text);
    }

    [WpfFact]
    public void Shows_a_link_as_its_text_without_the_address()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = "See [the docs](https://example.com/docs) now";

        Assert.Equal("See the docs now", screen.Text);
    }

    [WpfTheory]
    [InlineData("See [the docs](https://example.com/docs)", "See the docs")]
    [InlineData("[the docs](https://example.com/docs) now", "the docs now")]
    [InlineData("[the docs](https://example.com/docs)", "the docs")]
    public void Shows_and_opens_a_link_at_the_end_the_start_or_alone(string markdown, string shown)
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = markdown;
        screen.Click("the docs");

        Assert.Equal(shown, screen.Text);
        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
    }

    [WpfTheory]
    [InlineData("See [the docs](https://example.com/docs)", "See the docs")]
    [InlineData("[the docs](https://example.com/docs) now", "the docs now")]
    [InlineData("[the docs](https://example.com/docs)", "the docs")]
    public void Names_itself_for_assistive_tools_by_its_shown_text_before_it_is_loaded_and_when_it_changes(string markdown, string shown)
    {
        var title = new FormattedTitle(Palette.Colors, new FakeTheme(), new FakeDefaultBrowser());
        AutomationProperties.SetName(title, "set by the caller");

        title.Markdown = markdown;

        Assert.Equal(shown, AutomationProperties.GetName(title));

        title.Markdown = "Now **bold** and [other](https://example.com/other)";

        Assert.Equal("Now bold and other", AutomationProperties.GetName(title));
    }

    [WpfFact]
    public void Shows_bold_text_bold_and_other_text_normal()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = "Buy **fresh** milk";

        Assert.Equal(FontWeights.Bold, screen.Run("fresh").FontWeight);
        Assert.NotEqual(FontWeights.Bold, screen.Run("milk").FontWeight);
    }

    [WpfFact]
    public void Shows_italic_text_italic()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = "Buy *fresh* milk";

        Assert.Equal(FontStyles.Italic, screen.Run("fresh").FontStyle);
        Assert.NotEqual(FontStyles.Italic, screen.Run("milk").FontStyle);
    }

    [WpfFact]
    public void Shows_struck_through_text_struck_through()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = "Buy ~~old~~ milk";

        Assert.True(screen.IsStruckThrough("old"));
        Assert.False(screen.IsStruckThrough("milk"));
    }

    [WpfFact]
    public void Shows_code_in_a_monospaced_font()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = "Run `build` now";

        Assert.True(IsMonospaced(screen.Run("build").FontFamily), "The code isn't in a monospaced font.");
        Assert.False(IsMonospaced(screen.Run("now").FontFamily), "The text around the code is monospaced too.");
    }

    [WpfFact]
    public void Shows_colored_text_in_the_palette_color_of_the_light_theme()
    {
        using var screen = new TitleScreen(dark: false);

        screen.Title.Markdown = "Do :red[urgent] work";

        Assert.Equal(Parse(Red.Light), screen.ColorOf("urgent"));
    }

    [WpfFact]
    public void Shows_colored_text_in_the_palette_color_of_the_dark_theme()
    {
        using var screen = new TitleScreen(dark: true);

        screen.Title.Markdown = "Do :red[urgent] work";

        Assert.Equal(Parse(Red.Dark), screen.ColorOf("urgent"));
    }

    [WpfFact]
    public void Switches_a_shown_title_to_the_other_theme_when_Windows_switches()
    {
        using var screen = new TitleScreen(dark: false);
        screen.Title.Markdown = "Do :red[urgent] work";

        screen.Theme.Switch(true);

        Assert.Equal(Parse(Red.Dark), screen.ColorOf("urgent"));
        Assert.Equal("Do urgent work", screen.Text);
    }

    [WpfFact]
    public void Stops_following_the_theme_once_it_is_off_screen()
    {
        var screen = new TitleScreen(dark: false);
        screen.Title.Markdown = "Do :red[urgent] work";
        screen.Close();

        screen.Theme.Switch(true);

        Assert.Equal(Parse(Red.Light), screen.ColorOf("urgent"));
    }

    [WpfFact]
    public void Setting_the_Markdown_again_replaces_what_is_shown()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = "First **one**";

        screen.Title.Markdown = "Second";

        Assert.Equal("Second", screen.Text);
    }

    [WpfFact]
    public void Clicking_a_web_link_opens_it_in_the_default_browser()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = "See [the docs](https://example.com/docs) now";

        screen.Click("the docs");

        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
        Assert.Empty(screen.OtherLinks);
    }

    [WpfTheory]
    [InlineData("tasks/01900000-0000-7000-8000-000000000001")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/secret.txt")]
    [InlineData("javascript:alert(1)")]
    public void Clicking_a_link_that_is_not_a_web_link_opens_nothing_and_reports_its_address(string address)
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = $"See [that]({address}) now";

        screen.Click("that");

        Assert.Empty(screen.Browser.Opened);
        Assert.Equal([address], screen.OtherLinks);
    }

    [WpfFact]
    public void A_browser_that_cannot_open_a_web_link_is_reported_not_thrown()
    {
        using var screen = new TitleScreen();
        var failure = new InvalidOperationException("No browser.");
        screen.Browser.Failure = failure;
        screen.Title.Markdown = "See [the docs](https://example.com/docs) now";

        screen.Click("the docs");

        Assert.Same(failure, Assert.Single(screen.OpenFailures));
        Assert.Empty(screen.OtherLinks);
    }

    private static PaletteColor Red => Palette.Colors.Single(color => color.Name == "red");

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static bool IsMonospaced(FontFamily family)
    {
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyphs))
        {
            return false;
        }

        return glyphs.AdvanceWidths[glyphs.CharacterToGlyphMap['i']] == glyphs.AdvanceWidths[glyphs.CharacterToGlyphMap['W']];
    }

    /// <summary>A title in a showing window over the shared fakes, with every event it raises recorded. Dispose it to close the window.</summary>
    private sealed class TitleScreen : IDisposable
    {
        private readonly Window window;

        public TitleScreen(bool dark = false)
        {
            Theme = new FakeTheme { IsDark = dark };
            Browser = new FakeDefaultBrowser();
            Title = new FormattedTitle(Palette.Colors, Theme, Browser);
            Title.OtherLinkClicked += (_, address) => OtherLinks.Add(address);
            Title.OpenFailed += (_, failure) => OpenFailures.Add(failure);
            window = new Window { Content = Title, Width = 480, Height = 120 };
            window.Show();
        }

        public FakeTheme Theme { get; }

        public FakeDefaultBrowser Browser { get; }

        public FormattedTitle Title { get; }

        public List<string> OtherLinks { get; } = [];

        public List<Exception> OpenFailures { get; } = [];

        public string Text => OpenWindows.TextOf(Title);

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

        /// <summary>Clicks the link whose text is shown, as assistive tools do.</summary>
        public void Click(string linkText)
        {
            var link = Runs().First(run => run.Text.Contains(linkText)).Parent;
            while (link is not null and not Hyperlink)
            {
                link = (link as TextElement)?.Parent;
            }

            HyperlinkActivation.Invoke((Hyperlink)link!);
        }

        /// <summary>Closes the window and returns once the title has raised Unloaded, so it is off screen as far as WPF is concerned.</summary>
        public void Close() => LoadSync.Close(window, Title);

        public void Dispose() => window.Close();

        private IEnumerable<Run> Runs() => Walk(Title.Inlines).OfType<Run>();

        private static IEnumerable<Inline> Walk(InlineCollection inlines)
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
            }
        }
    }
}
