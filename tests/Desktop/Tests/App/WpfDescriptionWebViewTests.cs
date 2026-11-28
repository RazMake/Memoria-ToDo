using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Serilog.Events;
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
        WriteImage(Path.Combine(harness.CloneFolder, "images", TaskId, "picture.png"), Png);
        var address = harness.Port.CloneAddress;

        harness.Port.Show($"<title>images</title><img src=\"{address}images/{TaskId}/picture.png\">");

        Assert.Equal(new Uri("https://clone.memoria-todo.example/"), address);
        Assert.Equal($"{address}images/{TaskId}/picture.png", await harness.NextClonedFileAddress());
    }

    [WpfFact]
    public async Task Shows_an_image_that_is_only_in_the_edit_images_folder_at_the_clone_address()
    {
        using var harness = new DescriptionWebViewHarness();
        WriteImage(Path.Combine(harness.EditImagesFolder, "images", TaskId, "waiting.png"), Png);
        var core = await harness.Started;

        harness.Port.Show(Page($"<img src=\"{harness.Port.CloneAddress}images/{TaskId}/waiting.png\">"));
        await harness.NextLoadedPageTitle();

        Assert.Equal(new[] { 1 }, await ImageWidths(core));
    }

    [WpfFact]
    public async Task Shows_the_clones_image_when_a_waiting_image_has_the_same_path()
    {
        using var harness = new DescriptionWebViewHarness();
        WriteImage(Path.Combine(harness.CloneFolder, "images", TaskId, "same.png"), Png);
        WriteImage(Path.Combine(harness.EditImagesFolder, "images", TaskId, "same.png"), PngOfWidth(3));
        var core = await harness.Started;

        harness.Port.Show(Page($"<img src=\"{harness.Port.CloneAddress}images/{TaskId}/same.png\">"));
        await harness.NextLoadedPageTitle();

        Assert.Equal(new[] { 1 }, await ImageWidths(core));
    }

    [WpfFact]
    public async Task Does_not_serve_a_path_that_escapes_the_image_folders_though_an_image_is_there()
    {
        using var harness = new DescriptionWebViewHarness();
        WriteImage(Path.Combine(harness.CloneFolder, "images", TaskId, "control.png"), Png);
        // Three levels up from a task's image folder is the folder holding both the clone and the edit images.
        WriteImage(Path.Combine(harness.CloneFolder, "..", "secret.png"), Png);
        var core = await harness.Started;

        harness.Port.Show(Page(
            $"<img src=\"{harness.Port.CloneAddress}images/{TaskId}/control.png\">"
            + $"<img src=\"{harness.Port.CloneAddress}images/{TaskId}/..%2F..%2F..%2Fsecret.png\">"));
        await harness.NextLoadedPageTitle();

        Assert.Equal(new[] { 1, 0 }, await ImageWidths(core));
    }

    [WpfFact]
    public async Task Does_not_serve_an_image_whose_path_is_requested_with_an_escaped_character_though_the_unescaped_path_has_one()
    {
        using var harness = new DescriptionWebViewHarness();
        WriteImage(Path.Combine(harness.CloneFolder, "images", TaskId, "control.png"), Png);
        WriteImage(Path.Combine(harness.CloneFolder, "images", TaskId, "a.png"), Png);
        var core = await harness.Started;

        // The browser sends %61 as it is, and the design answers an escaped path with 404, though it names a.png.
        harness.Port.Show(Page(
            $"<img src=\"{harness.Port.CloneAddress}images/{TaskId}/control.png\">"
            + $"<img src=\"{harness.Port.CloneAddress}images/{TaskId}/%61.png\">"));
        await harness.NextLoadedPageTitle();

        Assert.Equal(new[] { 1, 0 }, await ImageWidths(core));
    }

    [WpfFact]
    public async Task Does_not_serve_an_image_outside_the_task_image_folders_though_the_clone_holds_it()
    {
        using var harness = new DescriptionWebViewHarness();
        WriteImage(Path.Combine(harness.CloneFolder, "images", TaskId, "control.png"), Png);
        WriteImage(Path.Combine(harness.CloneFolder, "other", "picture.png"), Png);
        var core = await harness.Started;

        harness.Port.Show(Page(
            $"<img src=\"{harness.Port.CloneAddress}images/{TaskId}/control.png\">"
            + $"<img src=\"{harness.Port.CloneAddress}other/picture.png\">"));
        await harness.NextLoadedPageTitle();

        Assert.Equal(new[] { 1, 0 }, await ImageWidths(core));
    }

    private const string TaskId = "0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";

    private static void WriteImage(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, bytes);
    }

    // A PNG that is the given number of pixels wide, so a test can tell which of two images the browser got.
    private static byte[] PngOfWidth(int width)
    {
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
            width, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[width * 4], width * 4)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // The natural width of each image of the page, once the page has loaded: 0 for an image the browser could not get.
    private static async Task<int[]> ImageWidths(Microsoft.Web.WebView2.Core.CoreWebView2 core) =>
        System.Text.Json.JsonSerializer.Deserialize<int[]>(
            await core.ExecuteScriptAsync("[...document.images].map(image => image.naturalWidth)"))!;

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

    [WpfFact]
    public void Creating_the_view_without_a_logger_is_refused()
    {
        var files = new FakeFileSystem();
        var rules = TestTaskStore.Create(files, new FakeImageEncoder(), new FakeTimeProvider(), @"C:\web-view-tests\device").Synchronization.Files;

        var error = Assert.Throws<ArgumentNullException>(
            () => new WpfDescriptionWebView(null, @"C:\web-view-tests\edit-images", @"C:\web-view-tests\browser-data", files, rules, null!));

        Assert.Equal("logger", error.ParamName);
    }

    [WpfFact]
    public async Task Logs_one_problem_entry_with_the_exception_when_a_page_over_the_browsers_size_limit_is_asked_for()
    {
        using var harness = new DescriptionWebViewHarness();
        harness.Port.Show(Page("<p>small</p>"));
        await harness.NextLoadedPageTitle();

        harness.Port.Show(PageOverTheBrowsersLimit());
        await harness.NextFailure();
        harness.Port.Show(PageOverTheBrowsersLimit());

        var problem = Assert.Single(harness.Log.Events, entry => entry.Level >= LogEventLevel.Warning);
        Assert.NotNull(problem.Exception);
    }

    [WpfFact]
    public async Task Logs_no_problem_for_a_page_that_loads()
    {
        using var harness = new DescriptionWebViewHarness();

        harness.Port.Show(Page("<p>small</p>"));
        await harness.NextLoadedPageTitle();

        harness.Log.Events.AssertNoProblems();
    }

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
