using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Platform.Tests;
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

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_link_text_in_the_text_look_link_color_and_the_text_around_it_in_the_text_color(bool dark)
    {
        using var screen = new TitleScreen(dark);

        screen.Title.Markdown = "See [the docs](https://example.com/docs) now";

        Assert.Equal(Parse(TextLooks.Of(dark).Link), screen.ColorOf("the docs"));
        Assert.False(screen.HasOwnColor("See"), "The text around the link has its own color.");
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_code_in_the_text_look_code_color_on_its_faded_background(bool dark)
    {
        using var screen = new TitleScreen(dark);

        screen.Title.Markdown = "Buy `fresh` milk";

        var look = TextLooks.Of(dark);
        Assert.Equal(Parse(TextLooks.Of(dark).Code), screen.ColorOf("fresh"));
        var background = Assert.IsType<SolidColorBrush>(screen.Run("fresh").Background);
        Assert.Equal(Parse(look.CodeBackground).R, background.Color.R);
        Assert.Equal(Parse(look.CodeBackground).G, background.Color.G);
        Assert.Equal(Parse(look.CodeBackground).B, background.Color.B);
        Assert.Equal(look.CodeBackgroundOpacity, background.Opacity * background.Color.A / 255, 2);
        Assert.Null(screen.Run("milk").Background);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_italic_text_in_the_text_look_italic_color(bool dark)
    {
        using var screen = new TitleScreen(dark);

        screen.Title.Markdown = "Buy _fresh_ milk";

        Assert.Equal(Parse(TextLooks.Of(dark).Italic), screen.ColorOf("fresh"));
        Assert.False(screen.HasOwnColor("milk"), "The text around the italic text has its own color.");
    }

    [WpfFact]
    public void Switches_link_code_and_italic_colors_to_the_other_theme_when_Windows_switches()
    {
        using var screen = new TitleScreen(dark: false);
        screen.Title.Markdown = "[docs](https://example.com/d) `code` _slant_";

        screen.Theme.Switch(true);

        Assert.Equal(Parse(TextLooks.Dark.Link), screen.ColorOf("docs"));
        Assert.Equal(Parse(TextLooks.Dark.Code), screen.ColorOf("code"));
        Assert.Equal(Parse(TextLooks.Dark.Italic), screen.ColorOf("slant"));
        var background = Assert.IsType<SolidColorBrush>(screen.Run("code").Background);
        Assert.Equal(TextLooks.Dark.CodeBackgroundOpacity, background.Opacity * background.Color.A / 255, 2);

        screen.Theme.Switch(false);

        Assert.Equal(Parse(TextLooks.Light.Link), screen.ColorOf("docs"));
        Assert.Equal(Parse(TextLooks.Light.Code), screen.ColorOf("code"));
        Assert.Equal(Parse(TextLooks.Light.Italic), screen.ColorOf("slant"));
    }

    [WpfFact]
    public void Shows_a_palette_color_over_the_italic_and_code_colors()
    {
        using var screen = new TitleScreen(dark: true);

        screen.Title.Markdown = "Do :red[_urgent_] and :red[`soon`] work";

        Assert.Equal(Parse(Red.Dark), screen.ColorOf("urgent"));
        Assert.Equal(Parse(Red.Dark), screen.ColorOf("soon"));
    }

    [WpfFact]
    public void Clicking_a_web_link_does_not_raise_the_mouse_click_to_the_element_around_the_title()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = "See [the docs](https://example.com/docs) now";

        screen.MouseClickOn("the docs");

        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
        Assert.Equal(0, screen.ParentClicks);
    }

    [WpfFact]
    public void Clicking_a_link_to_another_task_does_not_raise_the_mouse_click_to_the_element_around_the_title()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = "See [that task](task:abc) now";

        screen.MouseClickOn("that task");

        Assert.Equal(["task:abc"], screen.OtherLinks);
        Assert.Equal(0, screen.ParentClicks);
    }

    [WpfFact]
    public void Clicking_text_that_is_not_a_link_raises_the_mouse_click_to_the_element_around_the_title()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = "See [the docs](https://example.com/docs) now";

        screen.MouseClickOn("now");

        Assert.True(screen.ParentClicks > 0, "The click didn't reach the element around the title.");
        Assert.Empty(screen.Browser.Opened);
    }

    private const string Mention = "[Ana](contact:a1 \"{firstName}\")";

    private const string Escaped = "[Ana Pop \\(SDE\\)](contact:a1 \"{fullName} ({careerPath.short})\")";

    [WpfFact]
    public void Shows_a_mention_whose_text_holds_escaped_punctuation_as_one_chip_with_that_text()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = $"Ask {Escaped} now";

        var chip = Assert.Single(screen.Title.Chips());
        Assert.Equal("Ana Pop (SDE)", TitleChips.TextOf(chip));
        Assert.Equal("Ask Ana Pop (SDE) now", screen.Text);
        Assert.Equal("Ask Ana Pop (SDE) now", AutomationProperties.GetName(screen.Title));
    }

    [WpfTheory]
    [InlineData("bold", "[Ana **Pop** Li](contact:a1 \"{fullName}\")")]
    [InlineData("italic", "[Ana *Pop* Li](contact:a1 \"{fullName}\")")]
    public void Shows_a_mention_with_bold_or_italic_on_part_of_its_text_as_one_chip_keeping_that_style_on_that_part(string style, string mention)
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = $"Ask {mention} now";

        var chip = Assert.Single(screen.Title.Chips());
        Assert.Equal("Ana Pop Li", TitleChips.TextOf(chip));
        Assert.True(HasStyle(style, screen, "Pop"), $"'Pop' isn't {style}.");
        Assert.False(HasStyle(style, screen, "Ana"), $"'Ana' is {style} too.");
        Assert.False(HasStyle(style, screen, "Li"), $"'Li' is {style} too.");
    }

    [WpfFact]
    public void Shows_each_of_two_mentions_as_its_own_chip_and_the_text_around_them_outside_any_chip()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = $"Ask {Escaped} and {Bo} now";

        var chips = screen.Title.Chips();
        Assert.Equal(["Ana Pop (SDE)", "Bo Li"], chips.Select(TitleChips.TextOf));
        Assert.Equal("Ask Ana Pop (SDE) and Bo Li now", screen.Text);
        Assert.DoesNotContain(chips, chip => TitleChips.TextOf(chip).Contains("Ask") || TitleChips.TextOf(chip).Contains("now"));
    }

    [WpfFact]
    public async Task Makes_the_card_of_a_mention_with_escaped_punctuation_for_any_point_on_its_one_chip_with_that_chip_as_the_box()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Ask {Escaped} now";
        var chip = screen.ChipBox("Ana Pop (SDE)");
        var y = chip.Top + (chip.Height / 2);

        foreach (var x in new[] { chip.Left + 1, chip.Left + (chip.Width * 0.25), chip.Left + (chip.Width / 2), chip.Left + (chip.Width * 0.75), chip.Right - 1 })
        {
            var part = await screen.Title.PartAt(new Point(x, y));

            Assert.Equal("contact:a1", ((Border)part!.Card).Tag);
            Assert.InRange(part.Box.Left, chip.Left - 0.5, chip.Left + 0.5);
            Assert.InRange(part.Box.Top, chip.Top - 0.5, chip.Top + 0.5);
            Assert.InRange(part.Box.Right, chip.Right - 0.5, chip.Right + 0.5);
            Assert.InRange(part.Box.Bottom, chip.Bottom - 0.5, chip.Bottom + 0.5);
        }
    }

    [WpfFact]
    public async Task Makes_the_card_of_a_mention_with_a_bold_part_for_any_point_on_its_one_chip()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = "Ask [Ana **Pop** Li](contact:a1 \"{fullName}\") now";
        var chip = screen.ChipBox("Ana Pop Li");
        var y = chip.Top + (chip.Height / 2);

        foreach (var x in new[] { chip.Left + 1, chip.Left + (chip.Width / 2), chip.Right - 1 })
        {
            var part = await screen.Title.PartAt(new Point(x, y));

            Assert.Equal("contact:a1", ((Border)part!.Card).Tag);
            Assert.InRange(part.Box.Left, chip.Left - 0.5, chip.Left + 0.5);
            Assert.InRange(part.Box.Right, chip.Right - 0.5, chip.Right + 0.5);
        }
    }

    [WpfFact]
    public void Shows_a_mention_as_its_text_alone_with_its_markers_hidden()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = $"Ask {Mention} now";

        Assert.Equal("Ask Ana now", screen.Text);
        Assert.Equal("Ask Ana now", AutomationProperties.GetName(screen.Title));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_a_mention_on_the_text_look_mention_background_in_the_normal_text_color(bool dark)
    {
        using var screen = new TitleScreen(dark);

        screen.Title.Markdown = $"Ask {Mention} now";

        var expected = Parse(TextLooks.Of(dark).MentionBackground);
        var background = Assert.IsType<SolidColorBrush>(screen.Chip("Ana").Background);
        Assert.Equal(expected.R, background.Color.R);
        Assert.Equal(expected.G, background.Color.G);
        Assert.Equal(expected.B, background.Color.B);
        Assert.Equal(1.0, background.Opacity * background.Color.A / 255, 2);
        Assert.False(screen.HasOwnColor("Ana"), "The mention has a color of its own.");
        Assert.Null(screen.Run("now").Background);
    }

    [WpfFact]
    public void Switches_a_mention_background_to_the_other_theme_when_Windows_switches()
    {
        using var screen = new TitleScreen(dark: false);
        screen.Title.Markdown = $"Ask {Mention} now";

        screen.Theme.Switch(true);

        Assert.Equal(Parse(TextLooks.Dark.MentionBackground), Assert.IsType<SolidColorBrush>(screen.Chip("Ana").Background).Color);

        screen.Theme.Switch(false);

        Assert.Equal(Parse(TextLooks.Light.MentionBackground), Assert.IsType<SolidColorBrush>(screen.Chip("Ana").Background).Color);
    }

    [WpfFact]
    public void Shows_a_mention_without_making_it_a_link()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = $"Ask {Mention} now";

        Assert.False(screen.IsInLink("Ana"), "The mention is a link.");
    }

    [WpfFact]
    public void Keeps_a_web_link_in_the_same_title_as_a_link_beside_a_mention()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = $"Ask {Mention} about [the docs](https://example.com/docs)";

        Assert.True(screen.IsInLink("the docs"), "The web link isn't a link.");
        Assert.False(screen.IsInLink("Ana"), "The mention is a link.");
        Assert.Null(screen.Run("the docs").Background);
    }

    [WpfFact]
    public void Clicking_a_mention_raises_the_mouse_click_to_the_element_around_the_title_and_opens_nothing()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = $"Ask {Mention} now";

        screen.MouseClickOn("Ana");

        Assert.True(screen.ParentClicks > 0, "The click didn't reach the element around the title.");
        Assert.Empty(screen.Browser.Opened);
        Assert.Empty(screen.OtherLinks);
        Assert.Empty(screen.OpenFailures);
    }

    private const string Bo = "[Bo Li](contact:b2 \"{fullName}\")";

    private static Border CardOf(string name) => new() { Width = 60, Height = 24, Tag = name };

    private static Point Middle(Rect box) => new(box.Left + (box.Width / 2), box.Top + (box.Height / 2));

    [WpfFact]
    public async Task Makes_the_card_of_the_mention_chip_under_the_point_from_its_address_and_template_and_returns_it_with_the_chip_background_as_the_box()
    {
        using var screen = new TitleScreen();
        var asked = new List<Mention>();
        screen.Title.MentionCard = mention => { asked.Add(mention); return CardOf(mention.Address); };
        screen.Title.Markdown = $"Ask {Mention} now";
        var chip = screen.ChipBox("Ana");

        var part = await screen.Title.PartAt(Middle(chip));

        var only = Assert.Single(asked);
        Assert.Equal("contact:a1", only.Address);
        Assert.Equal("{firstName}", only.Template);
        Assert.NotNull(part);
        Assert.Equal("contact:a1", ((Border)part.Card).Tag);
        Assert.InRange(part.Box.Left, chip.Left - 0.5, chip.Left + 0.5);
        Assert.InRange(part.Box.Top, chip.Top - 0.5, chip.Top + 0.5);
        Assert.InRange(part.Box.Right, chip.Right - 0.5, chip.Right + 0.5);
        Assert.InRange(part.Box.Bottom, chip.Bottom - 0.5, chip.Bottom + 0.5);
    }

    [WpfFact]
    public async Task Makes_the_card_of_the_mention_for_a_point_on_the_chip_padding_beside_its_text()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Ask {Mention} now";
        var chip = screen.ChipBox("Ana");

        var onLeftPadding = await screen.Title.PartAt(new Point(chip.Left + 1, chip.Top + (chip.Height / 2)));
        var onRightPadding = await screen.Title.PartAt(new Point(chip.Right - 1, chip.Top + (chip.Height / 2)));

        Assert.Equal("contact:a1", ((Border)onLeftPadding!.Card).Tag);
        Assert.Equal("contact:a1", ((Border)onRightPadding!.Card).Tag);
    }

    [WpfFact]
    public async Task Makes_the_card_of_the_mention_the_point_is_on_when_a_title_has_text_around_and_two_mentions()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Hi {Mention} and {Bo} bye";

        var second = await screen.Title.PartAt(Middle(screen.ChipBox("Bo Li")));
        var first = await screen.Title.PartAt(Middle(screen.ChipBox("Ana")));

        Assert.Equal("contact:b2", ((Border)second!.Card).Tag);
        Assert.Equal("contact:a1", ((Border)first!.Card).Tag);
        Assert.NotEqual(first.Box, second.Box);
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_point_on_the_text_before_between_and_after_two_mentions()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Hi {Mention} and {Bo} bye";

        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("Hi")));
        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("and")));
        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("bye")));
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_point_on_text_that_is_not_a_mention()
    {
        using var screen = new TitleScreen();
        var asked = 0;
        screen.Title.MentionCard = _ => { asked++; return CardOf("any"); };
        screen.Title.Markdown = $"Ask {Mention} about [the docs](https://example.com/docs)";

        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("Ask")));
        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("the docs")));
        Assert.Equal(0, asked);
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_point_below_the_text()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = _ => CardOf("any");
        screen.Title.Markdown = $"Ask {Mention}";

        Assert.Null(await screen.Title.PartAt(new Point(Middle(screen.ChipBox("Ana")).X, screen.Title.ActualHeight + 50)));
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_mention_whose_user_makes_none()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = _ => null;
        screen.Title.Markdown = $"Ask {Mention} now";

        Assert.Null(await screen.Title.PartAt(Middle(screen.ChipBox("Ana"))));
    }

    [WpfFact]
    public async Task Makes_no_card_before_a_card_function_is_set()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = $"Ask {Mention} now";

        Assert.Null(screen.Title.MentionCard);
        Assert.Null(await screen.Title.PartAt(Middle(screen.ChipBox("Ana"))));
    }

    [WpfFact]
    public async Task Makes_no_card_for_the_name_of_a_task_reference_where_mentions_are_plain_text()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = _ => CardOf("any");
        screen.Title.Markdown = "Ask Ana now";

        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("Ana")));
    }

    [WpfFact]
    public async Task Makes_the_card_for_the_Markdown_now_shown_after_the_title_changes()
    {
        using var screen = new TitleScreen();
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Ask {Mention} now";
        screen.Title.Markdown = $"Tell {Bo} now";

        var part = await screen.Title.PartAt(Middle(screen.ChipBox("Bo Li")));

        Assert.Equal("contact:b2", ((Border)part!.Card).Tag);
    }

    [WpfFact]
    public void Shows_the_card_under_a_mention_once_the_pointer_rests_on_it()
    {
        using var screen = new TitleScreen();
        var card = CardOf("a1");
        screen.Title.MentionCard = _ => card;
        screen.Title.Markdown = $"Ask {Mention} now";

        screen.RestOn(Middle(screen.ChipBox("Ana")));

        Assert.True(screen.WaitForShown(card), "The card didn't show.");
    }

    [WpfFact]
    public void Shows_the_card_of_the_second_mention_when_the_pointer_moves_on_to_it_and_rests()
    {
        using var screen = new TitleScreen();
        var cards = new Dictionary<string, Border> { ["contact:a1"] = CardOf("a1"), ["contact:b2"] = CardOf("b2") };
        screen.Title.MentionCard = mention => cards[mention.Address];
        screen.Title.Markdown = $"{Mention} and {Bo}";

        screen.RestOn(Middle(screen.ChipBox("Ana")));
        Assert.True(screen.WaitForShown(cards["contact:a1"]), "The first card didn't show.");
        screen.RestOn(Middle(screen.ChipBox("Bo Li")));

        Assert.True(screen.WaitForShown(cards["contact:b2"]), "The second card didn't show.");
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

        public TitleScreen(bool dark = false, double width = 480)
        {
            Theme = new FakeTheme { IsDark = dark };
            Browser = new FakeDefaultBrowser();
            Title = new FormattedTitle(Theme, Browser, Pointer, Serilog.Core.Logger.None);
            Title.OtherLinkClicked += (_, address) => OtherLinks.Add(address);
            Title.OpenFailed += (_, failure) => OpenFailures.Add(failure);

            // Stands for a row around the title, which reacts to a click on it.
            var around = new Border { Child = Title };
            around.MouseLeftButtonUp += (_, _) => ParentClicks++;
            around.MouseUp += (_, _) => ParentClicks++;
            window = new Window { Content = around, Width = width, Height = 160 };
            window.Show();
        }

        public FakeTheme Theme { get; }

        public FakeDefaultBrowser Browser { get; }

        public FakeDragPointer Pointer { get; } = new();

        public FormattedTitle Title { get; }

        public List<string> OtherLinks { get; } = [];

        public List<Exception> OpenFailures { get; } = [];

        /// <summary>How many mouse-up events reached the element around the title.</summary>
        public int ParentClicks { get; private set; }

        public string Text => string.Concat(Runs().Select(run => run.Text));

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

        private static Hyperlink? FindLink(TextElement run)
        {
            DependencyObject? element = run;
            while (element is not null and not Hyperlink)
            {
                element = (element as TextElement)?.Parent;
            }

            return element as Hyperlink;
        }

        /// <summary>
        /// A point in the title's coordinates just inside the start of the first run that holds the text, halfway down its line.
        /// For text in the title itself, not a mention's chip; use <see cref="ChipBox"/> for a mention.
        /// </summary>
        public Point InsideStartOf(string text)
        {
            window.UpdateLayout();
            var rect = Run(text).ContentStart.GetCharacterRect(LogicalDirection.Forward);
            return new Point(rect.X + 2, rect.Y + rect.Height / 2);
        }

        /// <summary>The chip that shows the mention with the text: its whole background, padding included, in the title's coordinates.</summary>
        public Rect ChipBox(string text)
        {
            window.UpdateLayout();
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

        private IEnumerable<Run> Runs() => TitleChips.Walk(Title.Inlines).OfType<Run>();

        /// <summary>The chip a mention shows as: the border around the mention's text.</summary>
        public Border Chip(string text) => Title.Chip(text);
    }
}
