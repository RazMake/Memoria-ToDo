using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

[UX]
[Collection(ScreenTests.Collection)]
public class FormattedDescriptionTests
{
    [WpfFact]
    public void Switches_a_shown_description_to_the_other_theme_when_Windows_switches()
    {
        using var screen = new DescriptionScreen(dark: false);
        screen.Description.Markdown = "Some **bold** text";

        screen.Theme.Switch(true);

        Assert.Equal(DescriptionPage.Of("Some **bold** text", dark: true, screen.WebView.CloneAddress), screen.WebView.Page);
    }

    [WpfFact]
    public void Stops_following_the_theme_once_it_is_off_screen()
    {
        var screen = new DescriptionScreen(dark: false);
        screen.Description.Markdown = "Some text";
        screen.Close();
        var shown = screen.WebView.Pages.Count;

        screen.Theme.Switch(true);

        Assert.Equal(shown, screen.WebView.Pages.Count);
    }

    [WpfFact]
    public void Clicking_a_web_link_opens_it_in_the_default_browser()
    {
        using var screen = new DescriptionScreen();

        screen.WebView.ClickLink("https://example.com/docs");

        Assert.Equal(["https://example.com/docs"], screen.Browser.Opened);
        Assert.Empty(screen.OtherLinks);
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

    [WpfFact]
    public void Reports_an_image_the_user_resized_with_its_position_and_width()
    {
        using var screen = new DescriptionScreen();
        screen.Description.IsPreview = true;

        screen.WebView.ResizeImage(2, 320);
        screen.WebView.ResizeImage(0, 150);

        Assert.Equal([new ImageResize(2, 320), new ImageResize(0, 150)], screen.Resizes);
    }

    [WpfFact]
    public void Resizing_an_image_does_not_change_the_Markdown_or_the_page_shown()
    {
        using var screen = new DescriptionScreen();
        screen.Description.IsPreview = true;
        screen.Description.Markdown = "![a](a.png) text";
        var shown = screen.WebView.Pages.Count;

        screen.WebView.ResizeImage(0, 200);

        Assert.Equal("![a](a.png) text", screen.Description.Markdown);
        Assert.Equal(shown, screen.WebView.Pages.Count);
    }

    [WpfFact]
    public void A_failed_web_view_reports_no_resize()
    {
        using var screen = new DescriptionScreen();
        screen.Description.IsPreview = true;
        screen.WebView.Fail("The WebView2 Runtime is missing.");

        screen.WebView.ResizeImage(0, 200);

        Assert.Empty(screen.Resizes);
    }

    [WpfFact]
    public void A_released_description_reports_no_resize()
    {
        using var screen = new DescriptionScreen();
        screen.Description.IsPreview = true;
        screen.Description.Dispose();

        screen.WebView.ResizeImage(0, 200);

        Assert.Empty(screen.Resizes);
    }

    [WpfFact]
    public void Is_not_a_preview_at_first_and_shows_the_page_without_resizable_images()
    {
        using var screen = new DescriptionScreen(dark: false);

        screen.Description.Markdown = "![a](a.png) text";

        Assert.False(screen.Description.IsPreview);
        Assert.Equal(DescriptionPage.Of("![a](a.png) text", dark: false, screen.WebView.CloneAddress), screen.WebView.Page);
    }

    [WpfFact]
    public void Becoming_a_preview_shows_the_page_with_resizable_images_for_the_current_Markdown()
    {
        using var screen = new DescriptionScreen(dark: true);
        screen.Description.Markdown = "![a](a.png) text";

        screen.Description.IsPreview = true;

        Assert.True(screen.Description.IsPreview);
        Assert.Equal(DescriptionPage.WithResizableImages("![a](a.png) text", dark: true, screen.WebView.CloneAddress), screen.WebView.Page);
    }

    [WpfFact]
    public void Leaving_preview_shows_the_page_without_resizable_images_again()
    {
        using var screen = new DescriptionScreen(dark: false);
        screen.Description.Markdown = "![a](a.png) text";
        screen.Description.IsPreview = true;

        screen.Description.IsPreview = false;

        Assert.Equal(DescriptionPage.Of("![a](a.png) text", dark: false, screen.WebView.CloneAddress), screen.WebView.Page);
    }

    [WpfFact]
    public void Reports_no_image_resize_while_not_a_preview()
    {
        using var screen = new DescriptionScreen();
        screen.Description.IsPreview = true;
        screen.Description.IsPreview = false;

        screen.WebView.ResizeImage(0, 200);

        Assert.Empty(screen.Resizes);
    }

    [WpfFact]
    public void In_preview_clicking_a_web_link_opens_no_browser()
    {
        using var screen = new DescriptionScreen();
        screen.Description.IsPreview = true;

        screen.WebView.ClickLink("https://example.com/docs");

        Assert.Empty(screen.Browser.Opened);
    }

    [WpfFact]
    public void In_preview_clicking_a_task_link_raises_no_OtherLinkClicked()
    {
        using var screen = new DescriptionScreen();
        screen.Description.IsPreview = true;

        screen.WebView.ClickLink("task:01900000-0000-7000-8000-000000000001");

        Assert.Empty(screen.OtherLinks);
    }

    private const string Ana = "[Ana](contact:a1 \"{firstName}\")";

    private const string Bo = "[Bo Li](contact:b2 \"{fullName}\")";

    private static Border CardOf(string name) => new() { Width = 60, Height = 24, Tag = name };

    private static readonly Rect MentionBox = new(40, 12, 30, 16);

    [WpfFact]
    public async Task Makes_the_card_of_the_mention_the_web_view_found_from_its_address_and_template_and_returns_it_with_the_mention_box()
    {
        using var screen = new DescriptionScreen();
        var asked = new List<Mention>();
        screen.Description.MentionCard = mention => { asked.Add(mention); return CardOf(mention.Address); };
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        var part = await screen.Description.PartAt(new Point(45, 20));

        var only = Assert.Single(asked);
        Assert.Equal("contact:a1", only.Address);
        Assert.Equal("{firstName}", only.Template);
        Assert.NotNull(part);
        Assert.Equal("contact:a1", ((Border)part.Card).Tag);
        Assert.Equal(screen.InDescription(MentionBox), part.Box);
    }

    [WpfFact]
    public async Task Asks_the_web_view_about_the_point_in_the_web_views_own_coordinates()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        var point = new Point(45, 20);

        await screen.Description.PartAt(point);

        Assert.Equal([screen.InWebView(point)], screen.WebView.MentionQueries);
    }

    [WpfFact]
    public async Task Maps_each_mention_number_to_the_mention_counted_in_the_same_order_as_the_page()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"{Ana} and {Bo} and {Ana}";

        screen.WebView.MentionUnderPointer = new MentionAt(1, MentionBox);
        var second = await screen.Description.PartAt(new Point(1, 1));
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        var first = await screen.Description.PartAt(new Point(1, 1));
        screen.WebView.MentionUnderPointer = new MentionAt(2, MentionBox);
        var third = await screen.Description.PartAt(new Point(1, 1));

        Assert.Equal("contact:b2", ((Border)second!.Card).Tag);
        Assert.Equal("contact:a1", ((Border)first!.Card).Tag);
        Assert.Equal("contact:a1", ((Border)third!.Card).Tag);
    }

    [WpfFact]
    public async Task Counts_a_mention_in_a_list_after_a_heading_by_its_place_in_the_document()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"# {Ana}\n\n- {Bo}";
        screen.WebView.MentionUnderPointer = new MentionAt(1, MentionBox);

        var part = await screen.Description.PartAt(new Point(1, 1));

        Assert.Equal("contact:b2", ((Border)part!.Card).Tag);
    }

    [WpfFact]
    public async Task Does_not_count_a_contact_link_that_is_not_a_mention_when_it_maps_the_number()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"[Cy](contact:c3) `{Ana}` {Bo}";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        var part = await screen.Description.PartAt(new Point(1, 1));

        Assert.Equal("contact:b2", ((Border)part!.Card).Tag);
    }

    [WpfFact]
    public async Task Makes_no_card_where_the_web_view_finds_no_mention()
    {
        using var screen = new DescriptionScreen();
        var asked = 0;
        screen.Description.MentionCard = _ => { asked++; return CardOf("any"); };
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = null;

        Assert.Null(await screen.Description.PartAt(new Point(200, 100)));
        Assert.Equal(0, asked);
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_mention_whose_user_makes_none()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = _ => null;
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        Assert.Null(await screen.Description.PartAt(new Point(45, 20)));
    }

    [WpfFact]
    public async Task Makes_no_card_before_a_card_function_is_set()
    {
        using var screen = new DescriptionScreen();
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        Assert.Null(screen.Description.MentionCard);
        Assert.Null(await screen.Description.PartAt(new Point(45, 20)));
    }

    [WpfFact]
    public async Task Makes_the_card_for_the_Markdown_now_shown_after_the_description_changes()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.Description.Markdown = $"Tell {Bo} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        var part = await screen.Description.PartAt(new Point(45, 20));

        Assert.Equal("contact:b2", ((Border)part!.Card).Tag);
    }

    [WpfFact]
    public async Task Shows_nothing_for_an_answer_that_comes_after_another_Markdown_was_shown()
    {
        using var screen = new DescriptionScreen();
        var asked = 0;
        screen.Description.MentionCard = mention => { asked++; return CardOf(mention.Address); };
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        screen.WebView.HoldMentionAnswers();
        var answer = screen.Description.PartAt(new Point(45, 20));

        screen.Description.Markdown = $"Tell {Bo} now";
        screen.WebView.ReleaseMentionAnswers();

        Assert.Null(await answer);
        Assert.Equal(0, asked);
    }

    [WpfFact]
    public async Task Shows_nothing_for_an_answer_that_comes_after_the_description_became_a_preview()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        screen.WebView.HoldMentionAnswers();
        var answer = screen.Description.PartAt(new Point(45, 20));

        screen.Description.IsPreview = true;
        screen.WebView.ReleaseMentionAnswers();

        Assert.Null(await answer);
    }

    [WpfFact]
    public async Task Shows_the_card_of_an_answer_that_comes_while_the_same_page_is_still_shown()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        screen.WebView.HoldMentionAnswers();
        var answer = screen.Description.PartAt(new Point(45, 20));

        screen.WebView.ReleaseMentionAnswers();

        Assert.Equal("contact:a1", ((Border)(await answer)!.Card).Tag);
    }

    [WpfFact]
    public async Task A_preview_shows_cards_too()
    {
        using var screen = new DescriptionScreen();
        screen.Description.IsPreview = true;
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        var part = await screen.Description.PartAt(new Point(45, 20));

        Assert.Equal("contact:a1", ((Border)part!.Card).Tag);
    }

    [WpfFact]
    public async Task Makes_no_card_once_the_web_view_failed_and_the_Markdown_shows_as_source()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        screen.WebView.Fail("The WebView2 Runtime is missing.");

        Assert.Null(await screen.Description.PartAt(new Point(45, 20)));
    }

    [WpfFact]
    public async Task Keeps_showing_the_formatted_page_after_a_lookup_that_found_nothing()
    {
        using var screen = new DescriptionScreen();
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {Ana} now";
        screen.WebView.MentionUnderPointer = null;

        var part = await screen.Description.PartAt(new Point(45, 20));

        Assert.Null(part);
        Assert.True(screen.WebView.Element.IsVisible, "The formatted page no longer shows.");
        Assert.DoesNotContain(screen.Texts, text => text.Contains("Descriptions can't show formatted"));
        Assert.Empty(screen.OpenFailures);
    }

    /// <summary>A description in a showing window over the shared fakes, with every event it raises recorded. Dispose it to close the window.</summary>
    private sealed class DescriptionScreen : IDisposable
    {
        private readonly Window window;

        public DescriptionScreen(bool dark = false, bool browserStarted = true)
        {
            Theme = new FakeTheme { IsDark = dark };
            Browser = new FakeDefaultBrowser();
            WebView = new FakeDescriptionWebView(browserStarted);
            Description = new FormattedDescription(WebView, Theme, Browser);
            Description.OtherLinkClicked += (_, address) => OtherLinks.Add(address);
            Description.OpenFailed += (_, failure) => OpenFailures.Add(failure);
            Description.ImageResized += (_, resize) => Resizes.Add(resize);
            window = new Window { Content = Description, Width = 480, Height = 240 };
            window.Show();
        }

        public FakeTheme Theme { get; }

        public FakeDefaultBrowser Browser { get; }

        public FakeDescriptionWebView WebView { get; }

        public FormattedDescription Description { get; }

        public List<string> OtherLinks { get; } = [];

        public List<Exception> OpenFailures { get; } = [];

        public List<ImageResize> Resizes { get; } = [];

        /// <summary>A box in the web view's coordinates, in the description's.</summary>
        public Rect InDescription(Rect inWebView) => WebView.Element.TransformToAncestor(Description).TransformBounds(inWebView);

        /// <summary>A point in the description's coordinates, in the web view's.</summary>
        public Point InWebView(Point inDescription) => Description.TransformToDescendant(WebView.Element).Transform(inDescription);

        /// <summary>The texts the description displays, including a text box's text.</summary>
        public IReadOnlyList<string> Texts =>
        [
            .. OpenWindows.Descendants(Description).Prepend(Description).Select(element => element switch
            {
                TextBlock block => OpenWindows.TextOf(block),
                TextBox box => box.Text,
                _ => null,
            }).OfType<string>(),
        ];

        /// <summary>Closes the window and returns once the description has raised Unloaded, so it is off screen as far as WPF is concerned.</summary>
        public void Close() => LoadSync.Close(window, Description);

        public void Dispose() => window.Close();
    }
}
