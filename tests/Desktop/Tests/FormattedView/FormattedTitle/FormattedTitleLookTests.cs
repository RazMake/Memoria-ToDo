using System.Windows.Media;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle.TitleScreen;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle;

/// <summary>How a title looks: each style on its own text, the text look's colors in both themes, and a mention's background. The title is laid out but never shown.</summary>
[Unit]
public class FormattedTitleLookTests
{
    [WpfTheory]
    [InlineData("bold", "Buy **fresh** milk")]
    [InlineData("italic", "Buy *fresh* milk")]
    [InlineData("struck through", "Buy ~~fresh~~ milk")]
    [InlineData("code", "Buy `fresh` milk")]
    public void Shows_styled_text_in_its_style_and_the_text_around_it_plain(string style, string markdown)
    {
        using var screen = new TitleScreen(inWindow: false);

        screen.Title.Markdown = markdown;

        Assert.True(HasStyle(style, screen, "fresh"), $"'fresh' isn't {style}.");
        Assert.False(HasStyle(style, screen, "milk"), $"'milk' is {style} too.");
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_link_text_in_the_text_look_link_color_and_the_text_around_it_in_the_text_color(bool dark)
    {
        using var screen = new TitleScreen(dark, inWindow: false);

        screen.Title.Markdown = "See [the docs](https://example.com/docs) now";

        Assert.Equal(Parse(TextLooks.Of(dark).Link), screen.ColorOf("the docs"));
        Assert.False(screen.HasOwnColor("See"), "The text around the link has its own color.");
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_code_in_the_text_look_code_color_on_its_faded_background(bool dark)
    {
        using var screen = new TitleScreen(dark, inWindow: false);

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
        using var screen = new TitleScreen(dark, inWindow: false);

        screen.Title.Markdown = "Buy _fresh_ milk";

        Assert.Equal(Parse(TextLooks.Of(dark).Italic), screen.ColorOf("fresh"));
        Assert.False(screen.HasOwnColor("milk"), "The text around the italic text has its own color.");
    }

    [WpfFact]
    public void Shows_a_palette_color_over_the_italic_and_code_colors()
    {
        using var screen = new TitleScreen(dark: true, inWindow: false);

        screen.Title.Markdown = "Do :red[_urgent_] and :red[`soon`] work";

        Assert.Equal(Parse(Red.Dark), screen.ColorOf("urgent"));
        Assert.Equal(Parse(Red.Dark), screen.ColorOf("soon"));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_a_mention_on_the_text_look_mention_background_in_the_normal_text_color(bool dark)
    {
        using var screen = new TitleScreen(dark, inWindow: false);

        screen.Title.Markdown = $"Ask {AnaMention} now";

        var expected = Parse(TextLooks.Of(dark).MentionBackground);
        var background = Assert.IsType<SolidColorBrush>(screen.Chip("Ana").Background);
        Assert.Equal(expected.R, background.Color.R);
        Assert.Equal(expected.G, background.Color.G);
        Assert.Equal(expected.B, background.Color.B);
        Assert.Equal(1.0, background.Opacity * background.Color.A / 255, 2);
        Assert.False(screen.HasOwnColor("Ana"), "The mention has a color of its own.");
        Assert.Null(screen.Run("now").Background);
    }
}
