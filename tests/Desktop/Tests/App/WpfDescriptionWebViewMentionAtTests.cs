using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Microsoft.Web.WebView2.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The real WebView2 adapter, <c>WpfDescriptionWebView</c>, finds the mention under a point of its element through
/// <c>IDescriptionWebView.MentionAtAsync</c>, with no script on the page. Pages are the real description pages of
/// <c>DescriptionPage</c>, so a mention is the span the app shows, with its <c>data-mention</c> number. The tests read each
/// span's place with a script of their own, which still runs when the page's scripts are off, and ask about points inside and
/// outside it. The point is sent to DevTools, not the mouse, so no desktop focus is needed.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class WpfDescriptionWebViewMentionAtTests
{
    private const string Ana = "[Ana](contact:a1 \"{firstName}\")";
    private const string Bo = "[Bo Li](contact:b2 \"{fullName}\")";

    [WpfFact]
    public async Task Finds_the_number_of_the_mention_under_a_point_inside_it()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"Ask {Ana} and {Bo} now");
        var second = await BoxOf(core, 1);

        var found = await harness.Port.MentionAtAsync(Center(second));

        Assert.NotNull(found);
        Assert.Equal(1, found.Mention);
    }

    [WpfFact]
    public async Task Finds_the_first_mention_as_number_0()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"Ask {Ana} and {Bo} now");

        var found = await harness.Port.MentionAtAsync(Center(await BoxOf(core, 0)));

        Assert.Equal(0, found?.Mention);
    }

    [WpfFact]
    public async Task Returns_the_mention_box_in_the_elements_coordinates()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"Ask {Ana} and {Bo} now");
        var expected = await BoxOf(core, 1);

        var found = await harness.Port.MentionAtAsync(Center(expected));

        Assert.NotNull(found);
        Assert.InRange(found.Box.X, expected.X - 2, expected.X + 2);
        Assert.InRange(found.Box.Y, expected.Y - 2, expected.Y + 2);
        Assert.InRange(found.Box.Width, expected.Width - 4, expected.Width + 4);
        Assert.InRange(found.Box.Height, expected.Height - 4, expected.Height + 4);
    }

    [WpfFact]
    public async Task Finds_the_mention_under_a_point_on_its_bold_text_inside_the_span()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, "Ask [**Ana**](contact:a1 \"{firstName}\") now");

        var found = await harness.Port.MentionAtAsync(Center(await BoxOf(core, 0)));

        Assert.Equal(0, found?.Mention);
    }

    [WpfFact]
    public async Task Finds_the_mention_in_a_list_item_by_its_number_in_the_document()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"# {Ana}\n\n- {Bo}");

        var found = await harness.Port.MentionAtAsync(Center(await BoxOf(core, 1)));

        Assert.Equal(1, found?.Mention);
    }

    [WpfFact]
    public async Task Finds_no_mention_under_a_point_on_text_that_is_not_one()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"Some plain words before {Ana}");
        var mention = await BoxOf(core, 0);
        var before = new Point(mention.X - 30, mention.Y + (mention.Height / 2));

        Assert.Null(await harness.Port.MentionAtAsync(before));
    }

    [WpfFact]
    public async Task Finds_no_mention_under_a_point_on_a_web_link_beside_one()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"{Ana} and [the docs](https://example.com/docs)");
        var mention = await BoxOf(core, 0);
        var link = await BoxOfScript(core, "document.querySelector('a')");

        Assert.Null(await harness.Port.MentionAtAsync(Center(link)));
        Assert.Equal(0, (await harness.Port.MentionAtAsync(Center(mention)))?.Mention);
    }

    [WpfFact]
    public async Task Finds_no_mention_under_a_point_below_the_page_content()
    {
        using var harness = new DescriptionWebViewHarness();
        await ShowPage(harness, $"Ask {Ana}");

        var below = new Point(harness.Control.ActualWidth - 3, harness.Control.ActualHeight - 3);

        Assert.Null(await harness.Port.MentionAtAsync(below));
    }

    [WpfFact]
    public async Task Finds_the_mentions_of_the_page_now_shown_after_another_page_replaces_the_first()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"Ask {Ana} and {Bo} now");
        harness.Port.Show(DescriptionPage.Of($"Just {Bo}", dark: false, harness.Port.CloneAddress));
        await harness.NextLoadedPageTitle();
        await PageSettled(harness);

        var found = await harness.Port.MentionAtAsync(Center(await BoxOf(core, 0)));

        Assert.Equal(0, found?.Mention);
        Assert.Null(await harness.Port.MentionAtAsync(new Point(harness.Control.ActualWidth - 3, 3)));
    }

    [WpfFact]
    public async Task Finds_no_mention_and_reports_no_problem_under_a_point_where_the_browser_has_no_node()
    {
        using var harness = new DescriptionWebViewHarness();
        await ShowPage(harness, $"Ask {Ana}");

        var found = await harness.Port.MentionAtAsync(new Point(-50, -50));

        Assert.Null(found);
        Assert.Empty(harness.Failures);
        harness.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public async Task Finds_no_mention_and_reports_no_problem_for_a_lookup_that_a_newer_page_made_stale()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"Ask {Ana}");
        var center = Center(await BoxOf(core, 0));

        var lookup = harness.Port.MentionAtAsync(center);
        harness.Port.Show(DescriptionPage.Of($"Just {Bo}", dark: false, harness.Port.CloneAddress));
        var found = await lookup;
        await harness.NextLoadedPageTitle();

        Assert.Null(found);
        Assert.Empty(harness.Failures);
        harness.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public async Task Finds_the_mention_at_its_place_on_screen_and_returns_its_box_scaled_when_the_page_is_zoomed_to_2()
    {
        using var harness = new DescriptionWebViewHarness();
        var core = await ShowPage(harness, $"Ask {Ana} and {Bo} now");
        harness.Control.ZoomFactor = 2;
        // The browser tells the page its new zoom a moment later; the page's own pixel ratio, the zoom times the screen's
        // scale, says when it has it.
        var ratio = 2 * VisualTreeHelper.GetDpi(harness.Control).DpiScaleX;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (Math.Abs(await PixelRatio(core) - ratio) > 0.001 && DateTime.UtcNow < deadline)
        {
            await Task.Yield();
        }

        Assert.Equal(ratio, await PixelRatio(core), 3);
        await PageSettled(harness);
        // The test's own script reads the span's box in CSS pixels, as laid out at this zoom; the screen shows each of them twice as big.
        var css = await BoxOf(core, 1);
        var onScreen = new Rect(css.X * 2, css.Y * 2, css.Width * 2, css.Height * 2);

        var found = await harness.Port.MentionAtAsync(Center(onScreen));

        Assert.NotNull(found);
        Assert.Equal(1, found.Mention);
        Assert.InRange(found.Box.X, onScreen.X - 4, onScreen.X + 4);
        Assert.InRange(found.Box.Y, onScreen.Y - 4, onScreen.Y + 4);
        Assert.InRange(found.Box.Width, onScreen.Width - 8, onScreen.Width + 8);
        Assert.InRange(found.Box.Height, onScreen.Height - 8, onScreen.Height + 8);
    }

    private static async Task<double> PixelRatio(CoreWebView2 core) =>
        double.Parse(await core.ExecuteScriptAsync("window.devicePixelRatio"), CultureInfo.InvariantCulture);

    private static async Task<CoreWebView2> ShowPage(DescriptionWebViewHarness harness, string markdown)
    {
        var core = await harness.Started;
        harness.Port.Show(DescriptionPage.Of(markdown, dark: false, harness.Port.CloneAddress));
        await harness.NextLoadedPageTitle();
        await PageSettled(harness);
        return core;
    }

    // The page's first layout is at a size the browser corrects a moment after it loads, which moves the mentions, so a test that
    // reads a mention's place first needs the layout to have settled. A lookup answers only once the element has its final size.
    private static Task PageSettled(DescriptionWebViewHarness harness) => harness.Port.MentionAtAsync(new Point(-1, -1));

    private static Point Center(Rect box) => new(box.X + (box.Width / 2), box.Y + (box.Height / 2));

    // The place of the mention span with the number, in the page's own coordinates, which are the element's.
    private static Task<Rect> BoxOf(CoreWebView2 core, int number) =>
        BoxOfScript(core, $"document.querySelector('[data-mention=\"{number}\"]')");

    private static async Task<Rect> BoxOfScript(CoreWebView2 core, string element)
    {
        var json = await core.ExecuteScriptAsync(
            $"(() => {{ const r = {element}.getBoundingClientRect(); return [r.left, r.top, r.width, r.height]; }})()");
        var box = JsonSerializer.Deserialize<double[]>(json) ?? throw new InvalidOperationException("The element has no place.");
        return new Rect(box[0], box[1], box[2], box[3]);
    }
}
