using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

[UX]
public class FormattedDescriptionTests
{
    [WpfFact]
    public void Shows_the_web_view_it_was_given()
    {
        using var screen = new DescriptionScreen();

        Assert.Same(screen.WebView.Element, screen.Description.Content);
    }

    [WpfFact]
    public void Shows_Renderings_light_page_for_the_Markdown_in_the_web_view()
    {
        using var screen = new DescriptionScreen(dark: false);

        screen.Description.Markdown = "Some **bold** text";

        Assert.Equal(DescriptionPage.Of("Some **bold** text", dark: false, screen.WebView.CloneAddress), screen.WebView.Page);
    }

    [WpfFact]
    public void Shows_Renderings_dark_page_when_the_theme_is_dark()
    {
        using var screen = new DescriptionScreen(dark: true);

        screen.Description.Markdown = "Some **bold** text";

        Assert.Equal(DescriptionPage.Of("Some **bold** text", dark: true, screen.WebView.CloneAddress), screen.WebView.Page);
    }

    [WpfFact]
    public void Builds_the_page_for_the_clone_address_the_web_view_gives()
    {
        using var screen = new DescriptionScreen();
        screen.WebView.CloneAddress = new Uri("https://other.fake.example/clone/");

        screen.Description.Markdown = "![a](images/x/y.png)";

        Assert.Equal(DescriptionPage.Of("![a](images/x/y.png)", dark: false, new Uri("https://other.fake.example/clone/")), screen.WebView.Page);
    }

    [WpfFact]
    public void Setting_the_Markdown_again_shows_the_new_page_in_place_of_the_old()
    {
        using var screen = new DescriptionScreen();
        screen.Description.Markdown = "First";

        screen.Description.Markdown = "Second";

        Assert.Equal(DescriptionPage.Of("Second", dark: false, screen.WebView.CloneAddress), screen.WebView.Page);
    }

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
    public void A_description_set_before_the_browser_started_shows_once_it_has()
    {
        using var screen = new DescriptionScreen(browserStarted: false);
        screen.Description.Markdown = "Waiting";
        Assert.Null(screen.WebView.Page);

        screen.WebView.Start();

        Assert.Equal(DescriptionPage.Of("Waiting", dark: false, screen.WebView.CloneAddress), screen.WebView.Page);
    }

    [WpfFact]
    public void Clicking_a_web_link_opens_it_in_the_default_browser()
    {
        using var screen = new DescriptionScreen();

        screen.WebView.ClickLink("https://example.com/docs");

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
        using var screen = new DescriptionScreen();

        screen.WebView.ClickLink(address);

        Assert.Empty(screen.Browser.Opened);
        Assert.Equal([address], screen.OtherLinks);
    }

    [WpfFact]
    public void A_browser_that_cannot_open_a_web_link_is_reported_not_thrown()
    {
        using var screen = new DescriptionScreen();
        var failure = new InvalidOperationException("No browser.");
        screen.Browser.Failure = failure;

        screen.WebView.ClickLink("https://example.com/docs");

        Assert.Same(failure, Assert.Single(screen.OpenFailures));
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
    public void After_the_web_view_failed_a_new_Markdown_shows_as_source()
    {
        using var screen = new DescriptionScreen();
        screen.WebView.Fail("The WebView2 Runtime is missing.");

        screen.Description.Markdown = "Next **one**";

        Assert.Contains("Next **one**", screen.Texts);
    }

    [WpfFact]
    public void A_web_view_that_fails_while_showing_a_new_Markdown_shows_that_Markdown_as_source_with_the_reason()
    {
        using var screen = new DescriptionScreen();
        screen.Description.Markdown = "Old **page**";
        screen.WebView.FailWhenShowing = "The WebView2 Runtime is missing.";

        screen.Description.Markdown = "New **page**";

        Assert.Contains("New **page**", screen.Texts);
        Assert.DoesNotContain("Old **page**", screen.Texts);
        Assert.Contains(screen.Texts, text => text.Contains("Descriptions can't show formatted: The WebView2 Runtime is missing."));
    }

    [WpfFact]
    public void Releasing_the_description_releases_the_web_view()
    {
        using var screen = new DescriptionScreen();

        screen.Description.Dispose();

        Assert.True(screen.WebView.IsDisposed);
    }

    [WpfFact]
    public void A_released_description_no_longer_follows_the_theme()
    {
        using var screen = new DescriptionScreen(dark: false);
        screen.Description.Markdown = "Some text";
        screen.Description.Dispose();
        var shown = screen.WebView.Pages.Count;

        screen.Theme.Switch(true);

        Assert.Equal(shown, screen.WebView.Pages.Count);
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
            window = new Window { Content = Description, Width = 480, Height = 240 };
            window.Show();
        }

        public FakeTheme Theme { get; }

        public FakeDefaultBrowser Browser { get; }

        public FakeDescriptionWebView WebView { get; }

        public FormattedDescription Description { get; }

        public List<string> OtherLinks { get; } = [];

        public List<Exception> OpenFailures { get; } = [];

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
