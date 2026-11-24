using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle.TitleScreen;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle;

/// <summary>
/// The formatted title in a showing window, along the paths a reader takes: reading it, clicking in it, changing the theme, reading
/// mentions, and resting the pointer on a mention. Each style's exact look, the chips' structure and which card a point makes are
/// in the unit tests of the title's look, chips and cards.
/// </summary>
[UX("FormattedTitle")]
[Collection(ScreenTests.Collection)]
public class FormattedTitlePathTests
{
    [WpfFact]
    public void Reading_a_styled_title_as_plain_text_that_names_itself_and_shows_its_styles()
    {
        using var screen = new TitleScreen();
        AutomationProperties.SetName(screen.Title, "set by the caller");

        screen.Title.Markdown = "Buy **fresh** `milk` :red[today] at [the shop](https://example.com/shop)";

        // The title reads as text with no Markdown characters, and names itself by that text.
        Assert.Equal("Buy fresh milk today at the shop", screen.Text);
        Assert.Equal("Buy fresh milk today at the shop", AutomationProperties.GetName(screen.Title));

        // Each style shows on its own text, and the text around it stays plain.
        Assert.True(HasStyle("bold", screen, "fresh"), "'fresh' isn't bold.");
        Assert.True(HasStyle("code", screen, "milk"), "'milk' isn't code.");
        Assert.Equal(Parse(Red.Light), screen.ColorOf("today"));
        Assert.Equal(Parse(TextLooks.Light.Link), screen.ColorOf("the shop"));
        Assert.False(HasStyle("bold", screen, "Buy"), "'Buy' is bold too.");
        Assert.False(screen.HasOwnColor("Buy"), "The text around the styled text has its own color.");
    }

    [WpfFact]
    public void Clicking_a_web_link_a_task_link_and_plain_text_in_a_title()
    {
        using var screen = new TitleScreen();
        screen.Title.Markdown = "See [the docs](https://example.com/docs) and [that task](task:abc) now";

        // A web link opens in the default browser, and the click stays with the link.
        screen.MouseClickOn("the docs");
        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
        Assert.Empty(screen.OtherLinks);
        Assert.Equal(0, screen.ParentClicks);

        // A link to another task is reported, opens no browser, and its click stays with the link.
        screen.MouseClickOn("that task");
        Assert.Equal(["task:abc"], screen.OtherLinks);
        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
        Assert.Equal(0, screen.ParentClicks);

        // Text that is not a link passes the click on to the element around the title.
        screen.MouseClickOn("now");
        Assert.True(screen.ParentClicks > 0, "The click didn't reach the element around the title.");
        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
    }

    [WpfFact]
    public void Switching_the_theme_changes_the_colors_of_a_shown_title_until_its_window_closes()
    {
        var screen = new TitleScreen(dark: false);
        screen.Title.Markdown = $"Do :red[urgent] work [docs](https://example.com/d) `code` _slant_ {AnaMention}";
        Assert.Equal(Parse(Red.Light), screen.ColorOf("urgent"));

        // Windows switches to dark: the palette color, link, code, italic text and the mention's background change; the text stays.
        screen.Theme.Switch(true);
        Assert.Equal(Parse(Red.Dark), screen.ColorOf("urgent"));
        Assert.Equal(Parse(TextLooks.Dark.Link), screen.ColorOf("docs"));
        Assert.Equal(Parse(TextLooks.Dark.Code), screen.ColorOf("code"));
        Assert.Equal(Parse(TextLooks.Dark.Italic), screen.ColorOf("slant"));
        Assert.Equal(Parse(TextLooks.Dark.MentionBackground), Assert.IsType<SolidColorBrush>(screen.Chip("Ana").Background).Color);
        Assert.Equal("Do urgent work docs code slant Ana", screen.Text);

        // And back to light.
        screen.Theme.Switch(false);
        Assert.Equal(Parse(Red.Light), screen.ColorOf("urgent"));
        Assert.Equal(Parse(TextLooks.Light.Link), screen.ColorOf("docs"));
        Assert.Equal(Parse(TextLooks.Light.Code), screen.ColorOf("code"));
        Assert.Equal(Parse(TextLooks.Light.Italic), screen.ColorOf("slant"));
        Assert.Equal(Parse(TextLooks.Light.MentionBackground), Assert.IsType<SolidColorBrush>(screen.Chip("Ana").Background).Color);

        // Once the window closes, the title is off screen and no longer follows the theme.
        screen.Close();
        screen.Theme.Switch(true);
        Assert.Equal(Parse(Red.Light), screen.ColorOf("urgent"));
        Assert.Equal(Parse(TextLooks.Light.MentionBackground), Assert.IsType<SolidColorBrush>(screen.Chip("Ana").Background).Color);
    }

    [WpfFact]
    public void Reading_a_title_with_two_mentions_beside_a_web_link_and_clicking_a_mention()
    {
        using var screen = new TitleScreen();

        screen.Title.Markdown = $"Ask {EscapedMention} and {BoMention} about [the docs](https://example.com/docs)";

        // Each mention is its own chip, shown as its text with the markers hidden; the text around them is outside any chip.
        var chips = screen.Title.Chips();
        Assert.Equal(["Ana Pop (SDE)", "Bo Li"], chips.Select(TitleChips.TextOf));
        Assert.DoesNotContain(chips, chip => TitleChips.TextOf(chip).Contains("Ask") || TitleChips.TextOf(chip).Contains("about"));
        Assert.Equal("Ask Ana Pop (SDE) and Bo Li about the docs", screen.Text);
        Assert.Equal("Ask Ana Pop (SDE) and Bo Li about the docs", AutomationProperties.GetName(screen.Title));

        // A mention is not a link, while the web link beside it is.
        Assert.False(screen.IsInLink("Ana Pop"), "The mention is a link.");
        Assert.False(screen.IsInLink("Bo Li"), "The mention is a link.");
        Assert.True(screen.IsInLink("the docs"), "The web link isn't a link.");
        Assert.Null(screen.Run("the docs").Background);

        // Clicking a mention opens nothing, and its click goes to the element around the title.
        screen.MouseClickOn("Bo Li");
        Assert.True(screen.ParentClicks > 0, "The click didn't reach the element around the title.");
        Assert.Empty(screen.Browser.Opened);
        Assert.Empty(screen.OtherLinks);
        Assert.Empty(screen.OpenFailures);
    }

    [WpfFact]
    public void Resting_the_pointer_on_a_mention_shows_its_card_and_moving_on_to_the_next_mention_shows_that_card()
    {
        using var screen = new TitleScreen();
        var cards = new Dictionary<string, Border> { ["contact:a1"] = CardOf("a1"), ["contact:b2"] = CardOf("b2") };
        screen.Title.MentionCard = mention => cards[mention.Address];
        screen.Title.Markdown = $"{AnaMention} and {BoMention}";

        screen.RestOn(Middle(screen.ChipBox("Ana")));
        Assert.True(screen.WaitForShown(cards["contact:a1"]), "The first card didn't show.");
        screen.RestOn(Middle(screen.ChipBox("Bo Li")));

        Assert.True(screen.WaitForShown(cards["contact:b2"]), "The second card didn't show.");
    }
}
