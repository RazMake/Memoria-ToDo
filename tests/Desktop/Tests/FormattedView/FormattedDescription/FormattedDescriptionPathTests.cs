using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Desktop.FormattedView.Tests.FormattedDescription.DescriptionScreen;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedDescription;

/// <summary>
/// The formatted description in a showing window over a fake web view, along the paths a reader takes: reading it and changing the theme,
/// clicking a link, a web view that can't start, previewing it and resizing its images, resting on a mention, and releasing it. Which
/// card a point makes and when no resize is reported are in the unit tests of the description's cards and resizes.
/// </summary>
[UX("FormattedDescription")]
[Collection(ScreenTests.Collection)]
public class FormattedDescriptionPathTests
{
    [WpfFact]
    public void Showing_the_Markdown_as_a_page_that_follows_the_theme_until_its_window_closes()
    {
        var screen = new DescriptionScreen(dark: false);

        // Not a preview at first: the page has no resizable images.
        screen.Description.Markdown = "Some **bold** text";
        Assert.False(screen.Description.IsPreview);
        Assert.Equal(DescriptionPage.Of("Some **bold** text", dark: false, screen.WebView.CloneAddress), screen.WebView.Page);

        // Windows switches to dark: the same Markdown shows in the dark page.
        screen.Theme.Switch(true);
        Assert.Equal(DescriptionPage.Of("Some **bold** text", dark: true, screen.WebView.CloneAddress), screen.WebView.Page);

        // Once the window closes, the description is off screen and shows no more pages.
        screen.Close();
        var shown = screen.WebView.Pages.Count;
        screen.Theme.Switch(false);
        Assert.Equal(shown, screen.WebView.Pages.Count);
    }

    [WpfFact]
    public void Clicking_a_web_link_opens_the_default_browser_and_clicking_a_task_link_is_reported()
    {
        using var screen = new DescriptionScreen();

        screen.WebView.ClickLink("https://example.com/docs");
        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
        Assert.Empty(screen.OtherLinks);

        screen.WebView.ClickLink("task:01900000-0000-7000-8000-000000000001");
        Assert.Equal(["task:01900000-0000-7000-8000-000000000001"], screen.OtherLinks);
        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
    }

    [WpfFact]
    public void A_web_view_that_cannot_start_shows_the_Markdown_source_with_the_reason()
    {
        using var screen = new DescriptionScreen();
        screen.Description.Markdown = "Some **bold** text";

        screen.WebView.Fail("The WebView2 Runtime is missing.");

        Assert.Contains("Some **bold** text", screen.Texts);
        Assert.Contains(screen.Texts, text => text.Contains("Descriptions can't show formatted: The WebView2 Runtime is missing."));
        Assert.False(screen.WebView.Element.IsVisible, "The failed web view still shows.");
    }

    [WpfFact]
    public void Previewing_the_description_reports_resized_images_and_ignores_links_until_the_preview_ends()
    {
        using var screen = new DescriptionScreen(dark: true);
        screen.Description.Markdown = "![a](a.png) text";

        // Becoming a preview shows the page with resizable images for the current Markdown.
        screen.Description.IsPreview = true;
        Assert.True(screen.Description.IsPreview);
        Assert.Equal(DescriptionPage.WithResizableImages("![a](a.png) text", dark: true, screen.WebView.CloneAddress), screen.WebView.Page);

        // A resized image is reported with its position and width; the Markdown and the page stay as they are.
        var shown = screen.WebView.Pages.Count;
        screen.WebView.ResizeImage(2, 320);
        screen.WebView.ResizeImage(0, 150);
        Assert.Equal([new ImageResize(2, 320), new ImageResize(0, 150)], screen.Resizes);
        Assert.Equal("![a](a.png) text", screen.Description.Markdown);
        Assert.Equal(shown, screen.WebView.Pages.Count);

        // In a preview, clicked links do nothing.
        screen.WebView.ClickLink("https://example.com/docs");
        screen.WebView.ClickLink("task:01900000-0000-7000-8000-000000000001");
        Assert.Empty(screen.Browser.Opened);
        Assert.Empty(screen.OtherLinks);

        // Leaving the preview shows the page without resizable images again, and a resize is no longer reported.
        screen.Description.IsPreview = false;
        Assert.Equal(DescriptionPage.Of("![a](a.png) text", dark: true, screen.WebView.CloneAddress), screen.WebView.Page);
        screen.WebView.ResizeImage(0, 200);
        Assert.Equal(2, screen.Resizes.Count);
    }

    [WpfFact]
    public async Task Resting_on_a_mention_the_web_view_found_makes_its_card_in_a_preview_and_for_a_changed_Markdown_too()
    {
        using var screen = new DescriptionScreen();
        var asked = new List<Mention>();
        screen.Description.MentionCard = mention => { asked.Add(mention); return CardOf(mention.Address); };
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        // The card is made from the mention's address and template, boxed where the web view found the mention.
        var part = await screen.Description.PartAt(new Point(45, 20));
        var only = Assert.Single(asked);
        Assert.Equal("contact:a1", only.Address);
        Assert.Equal("{firstName}", only.Template);
        Assert.NotNull(part);
        Assert.Equal("contact:a1", ((Border)part.Card).Tag);
        Assert.Equal(screen.InDescription(MentionBox), part.Box);

        // A preview shows cards too.
        screen.Description.IsPreview = true;
        Assert.Equal("contact:a1", ((Border)(await screen.Description.PartAt(new Point(45, 20)))!.Card).Tag);

        // The Markdown now shown decides the card.
        screen.Description.Markdown = $"Tell {BoMention} now";
        Assert.Equal("contact:b2", ((Border)(await screen.Description.PartAt(new Point(45, 20)))!.Card).Tag);
    }

    [WpfFact]
    public void Releasing_the_description_releases_the_web_view_and_stops_following_the_theme()
    {
        using var screen = new DescriptionScreen(dark: false);
        screen.Description.Markdown = "Some text";
        screen.Description.Dispose();
        var shown = screen.WebView.Pages.Count;

        screen.Theme.Switch(true);

        Assert.True(screen.WebView.IsDisposed);
        Assert.Equal(shown, screen.WebView.Pages.Count);
    }
}
