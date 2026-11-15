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
[Collection(ScreenTests.Collection)]
public class FormattedTitleTests
{
    [WpfFact]
    public void Shows_the_title_as_text_with_no_Markdown_characters_and_names_itself_by_that_text()
    {
        using var screen = new TitleScreen();
        AutomationProperties.SetName(screen.Title, "set by the caller");

        screen.Title.Markdown = "Buy **fresh** `milk` :red[today] at [the shop](https://example.com/shop)";

        Assert.Equal("Buy fresh milk today at the shop", screen.Text);
        Assert.Equal("Buy fresh milk today at the shop", AutomationProperties.GetName(screen.Title));
    }

    [WpfTheory]
    [InlineData("bold", "Buy **fresh** milk")]
    [InlineData("italic", "Buy *fresh* milk")]
    [InlineData("struck through", "Buy ~~fresh~~ milk")]
    [InlineData("code", "Buy `fresh` milk")]
    public void Shows_styled_text_in_its_style_and_the_text_around_it_plain(string style, string markdown)
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = markdown;

        Assert.True(HasStyle(style, screen, "fresh"), $"'fresh' isn't {style}.");
        Assert.False(HasStyle(style, screen, "milk"), $"'milk' is {style} too.");
    }

    [WpfFact]
    public void Switches_a_shown_title_to_the_other_theme_when_Windows_switches()
    {
        using var screen = new TitleScreen(dark: false);
        screen.Title.Markdown = "Do :red[urgent] work";
        Assert.Equal(Parse(Red.Light), screen.ColorOf("urgent"));

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
    public void Clicking_a_web_link_opens_it_in_the_default_browser()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = "See [the docs](https://example.com/docs) now";

        screen.Click("the docs");

        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
        Assert.Empty(screen.OtherLinks);
    }

    private static PaletteColor Red => Palette.Colors.Single(color => color.Name == "red");

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static bool HasStyle(string style, TitleScreen screen, string text) => style switch
    {
        "bold" => screen.Run(text).FontWeight == FontWeights.Bold,
        "italic" => screen.Run(text).FontStyle == FontStyles.Italic,
        "struck through" => screen.IsStruckThrough(text),
        "code" => IsMonospaced(screen.Run(text).FontFamily),
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };

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
