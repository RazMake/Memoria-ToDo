using System.IO;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The WPF description web view adapter, <c>WpfDescriptionWebView</c>, through <c>IDescriptionWebView</c> and the
/// real WebView2 browser it shows in. Each test has its own clone and browser data folders under the temp folder.
/// A link is clicked with mouse input sent to the test's own browser through the DevTools protocol, so these
/// need no desktop focus and touch no other program. Never opens a real site: every link is cancelled by the adapter.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class WpfDescriptionWebViewTests
{
    private const string Title = "<title>{0}</title>";

    // A 1x1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    [WpfFact]
    public async Task Shows_only_the_latest_page_asked_for_before_the_browser_started_with_scripts_off()
    {
        using var harness = new DescriptionWebViewHarness();

        harness.Port.Show(string.Format(Title, "first"));
        harness.Port.Show(string.Format(Title, "second") + "<script>document.title = 'script ran';</script>");

        await harness.Started;
        var shown = await harness.NextLoadedPageTitle();

        Assert.Equal("second", shown);
    }

    [WpfFact]
    public async Task Loads_an_image_from_the_clone_folder_at_the_clone_address()
    {
        using var harness = new DescriptionWebViewHarness();
        File.WriteAllBytes(Path.Combine(harness.CloneFolder, "picture.png"), Png);
        var address = harness.Port.CloneAddress;

        harness.Port.Show($"<title>images</title><img src=\"{address}picture.png\">");

        Assert.Equal(new Uri("https://clone.memoria-todo.example/"), address);
        Assert.Equal($"{address}picture.png", await harness.NextClonedFileAddress());
    }

    [WpfFact]
    public async Task Cancels_every_link_the_user_clicks_including_one_for_a_new_window_and_reports_its_address()
    {
        using var harness = new DescriptionWebViewHarness();
        var address = harness.Port.CloneAddress;
        harness.Port.Show(
            string.Format(Title, "links")
            + "<a href=\"https://example.com/web\">web</a>"
            + $"<a href=\"{address}other.html\">clone page</a>"
            + "<a href=\"https://example.com/window\" target=\"_blank\">new window</a>");
        await harness.Started;
        await harness.NextLoadedPageTitle();

        var reported = new List<string>();
        for (var link = 0; link < 3; link++)
        {
            await harness.ClickLink(link);
            reported.Add(await harness.NextReportedLink());
        }

        Assert.Equal(["https://example.com/web", $"{address}other.html", "https://example.com/window"], reported);
        Assert.Equal(("https://example.com/web", true), await harness.NextUserNavigation());
        Assert.Equal(($"{address}other.html", true), await harness.NextUserNavigation());
        Assert.Equal(("https://example.com/window", true), await harness.NextNewWindow());
    }

    [WpfFact]
    public async Task Is_as_tall_as_its_page_so_long_content_is_not_cut_and_a_shorter_next_page_shrinks_it()
    {
        using var harness = new DescriptionWebViewHarness();
        await harness.Started;

        harness.Port.Show(Page("<div style=\"height:2000px\"></div>"));
        await harness.NextLoadedPageTitle();
        await harness.WaitForHeight((height, content) => Math.Abs(height - 2000) <= 1 && Math.Abs(content - 2000) <= 1);

        harness.Port.Show(Page("<div style=\"height:100px\"></div>"));
        await harness.NextLoadedPageTitle();
        await harness.WaitForHeight((height, content) => Math.Abs(height - 100) <= 1 && Math.Abs(content - 100) <= 1);
    }

    [WpfFact]
    public async Task Changes_height_with_its_width_so_wrapped_text_is_never_cut()
    {
        using var harness = new DescriptionWebViewHarness();
        await harness.Started;
        harness.Port.Show(Page("<p style=\"margin:0;font:16px/20px Arial\">" + string.Concat(Enumerable.Repeat("words ", 300)) + "</p>"));
        await harness.NextLoadedPageTitle();
        var medium = 0.0;
        await harness.WaitForHeight((height, content) => (medium = height) > 0 && Math.Abs(height - content) <= 1);

        harness.ResizeWindowTo(200);
        var narrow = 0.0;
        await harness.WaitForHeight((height, content) => height > medium && Math.Abs(height - content) <= 1 && (narrow = height) > 0);

        harness.ResizeWindowTo(800);
        await harness.WaitForHeight((height, content) => height < medium && Math.Abs(height - content) <= 1);
        Assert.True(narrow > medium);
    }

    // A page with no margin around its content and no script, so its content height is exactly what the test sets.
    private static string Page(string body) => $"<!DOCTYPE html><title>page</title><body style=\"margin:0\">{body}</body>";

    [WpfFact]
    public async Task Reports_one_failure_when_a_page_over_the_browsers_size_limit_is_asked_for_after_the_browser_started()
    {
        using var harness = new DescriptionWebViewHarness();
        harness.Port.Show(Page("<p>small</p>"));
        await harness.NextLoadedPageTitle();

        harness.Port.Show(PageOverTheBrowsersLimit());

        Assert.False(string.IsNullOrWhiteSpace(await harness.NextFailure()));
        AssertOnlyOneFailureAndStillDisposable(harness);
    }

    // The browser accepts at most 2 MB of page (its NavigateToString documentation); this is well over that in characters and in bytes.
    private static string PageOverTheBrowsersLimit() => Page(new string('a', 3_000_000));

    private static void AssertOnlyOneFailureAndStillDisposable(DescriptionWebViewHarness harness)
    {
        // Asked for again after the failure: it must neither throw nor report a second failure.
        harness.Port.Show(PageOverTheBrowsersLimit());
        Assert.Single(harness.Failures);
        Assert.Empty(harness.Unhandled);
        harness.Port.Dispose();
    }

    [WpfFact]
    public async Task Releases_the_browser_when_disposed()
    {
        using var harness = new DescriptionWebViewHarness();
        await harness.Started;
        var browser = harness.Browser;

        harness.Port.Dispose();

        await browser.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(DescriptionWebViewHarness.Limit);
        Assert.True(browser.HasExited);
    }
}
