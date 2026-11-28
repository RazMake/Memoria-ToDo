using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform.Tests;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Microsoft.Web.WebView2.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The real WebView2 adapter, <c>WpfDescriptionWebView</c>, reports an image box the user resized, through
/// <c>IDescriptionWebView.ImageResized</c>. The user's drag of the box's corner is mouse input sent to the test's own
/// browser through the DevTools protocol, as the link tests do, so it needs no desktop focus. Pages are the real
/// description pages of <c>DescriptionPage</c>, so the box is the one the app shows.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class WpfDescriptionWebViewImageResizeTests
{
    private const int StartWidth = 200;
    private const int Drag = 60;

    // The window is 400 wide, which is as wide as a box can get, so the third drag is shorter.
    private const int ThirdDrag = 30;

    // A 1x1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static Task<ImageResize> Next(Channel<ImageResize> reports) =>
        reports.Reader.ReadAsync().AsTask().WaitAsync(DescriptionWebViewHarness.Limit);

    [WpfFact]
    public async Task Reports_the_image_number_and_the_new_width_when_the_user_drags_the_boxs_corner()
    {
        using var harness = new DescriptionWebViewHarness();
        var reports = Listen(harness);
        var core = await ShowImages(harness, $"![a|{StartWidth}](images/{TaskId}/one.png)");

        await DragCornerHoldingButton(harness, core, 0, Drag);

        var report = await Next(reports);
        Assert.Equal(0, report.Image);
        Assert.InRange(report.Width, StartWidth + Drag - 2, StartWidth + Drag + 2);
        Assert.Equal(await BoxWidth(core, 0), report.Width);
    }

    [WpfFact]
    public async Task Reports_the_number_of_the_image_that_was_dragged_not_of_the_first()
    {
        using var harness = new DescriptionWebViewHarness();
        var reports = Listen(harness);
        var core = await ShowImages(harness, $"![a|{StartWidth}](images/{TaskId}/one.png)\n\n![b|{StartWidth}](images/{TaskId}/two.png)", "one.png", "two.png");

        await DragCornerHoldingButton(harness, core, 1, Drag);

        Assert.Equal(1, (await Next(reports)).Image);
    }

    [WpfFact]
    public async Task Reports_once_for_each_finished_drag_not_for_each_step_of_it()
    {
        using var harness = new DescriptionWebViewHarness();
        var reports = Listen(harness);
        var core = await ShowImages(harness, $"![a|{StartWidth}](images/{TaskId}/one.png)");

        await DragCornerHoldingButton(harness, core, 0, Drag);
        var first = await Next(reports);
        await DragCornerHoldingButton(harness, core, 0, Drag);
        var second = await Next(reports);

        // A report made at a step of the first drag would come before the second drag's.
        Assert.InRange(first.Width, StartWidth + Drag - 2, StartWidth + Drag + 2);
        Assert.InRange(second.Width, StartWidth + 2 * Drag - 2, StartWidth + 2 * Drag + 2);
        Assert.False(reports.Reader.TryRead(out _));
    }

    [WpfFact]
    public async Task Reports_nothing_for_a_page_that_was_replaced_before_the_drag_ended()
    {
        var pause = new FakeDragPause();
        using var harness = new DescriptionWebViewHarness(pause, new InstantImageMeasure(new ImageResize(0, StartWidth + Drag)));
        var reports = Listen(harness);
        var core = await ShowImages(harness, $"![a|{StartWidth}](images/{TaskId}/one.png)");
        var (x, y) = await PressCornerAndDragHalfway(core);
        await pause.Running;

        // The old drag's pause passes after the page was replaced. Measuring is instant, so a stale measurement would report at
        // once, before this test looks, and not be overtaken by a later drag step.
        await ShowNextPage(harness);
        await Mouse(core, "mouseMoved", x + Drag, y, 1);
        await Mouse(core, "mouseReleased", x + Drag, y, 0);
        pause.RaiseTick();

        Assert.False(reports.Reader.TryRead(out _));
    }

    [WpfFact]
    public async Task Reports_one_final_width_for_a_drag_the_user_holds_and_goes_on_with_and_nothing_more_after_it()
    {
        using var harness = new DescriptionWebViewHarness();
        var reports = Listen(harness);
        var core = await ShowImages(harness, $"![a|{StartWidth}](images/{TaskId}/one.png)");
        using var held = HoldButton(harness);

        await DragCorner(core, 0, Drag);
        await DragCorner(core, 0, Drag);
        held.Dispose();

        var report = await Next(reports);
        Assert.InRange(report.Width, StartWidth + 2 * Drag - 2, StartWidth + 2 * Drag + 2);
        Assert.Equal(await BoxWidth(core, 0), report.Width);
        // A report that came after it, at the pause, would come before the next drag's own.
        await DragCornerHoldingButton(harness, core, 0, ThirdDrag);
        Assert.InRange((await Next(reports)).Width, StartWidth + 2 * Drag + ThirdDrag - 2, StartWidth + 2 * Drag + ThirdDrag + 2);
    }

    [WpfFact]
    public async Task Reports_nothing_for_a_page_replaced_during_a_drag_with_the_mouse_button_held_even_after_it_is_released()
    {
        var pause = new FakeDragPause { ButtonDown = true };
        using var harness = new DescriptionWebViewHarness(pause, new InstantImageMeasure(new ImageResize(0, StartWidth + Drag)));
        var reports = Listen(harness);
        var core = await ShowImages(harness, $"![a|{StartWidth}](images/{TaskId}/one.png)");
        var (x, y) = await PressCornerAndDragHalfway(core);
        await pause.Running;

        await ShowNextPage(harness);
        await Mouse(core, "mouseMoved", x + Drag, y, 1);
        // The old drag's pause passes with the button still held, then once more after the release, both after the page was replaced.
        pause.RaiseTick();
        pause.ButtonDown = false;
        await Mouse(core, "mouseReleased", x + Drag, y, 0);
        pause.RaiseTick();

        Assert.False(reports.Reader.TryRead(out _));
    }

    [WpfFact]
    public async Task Reports_nothing_from_a_measurement_a_new_drag_step_overtook()
    {
        var pause = new FakeDragPause();
        var measure = new HeldImageMeasure();
        using var harness = new DescriptionWebViewHarness(pause, measure);
        var reports = Listen(harness);
        var core = await ShowImages(harness, $"![a|{StartWidth}](images/{TaskId}/one.png)");
        await DragCorner(core, 0, Drag);

        // The first drag's pause passes with the button up, and its width is measured. A new drag's step comes in before
        // the measurement is used.
        await pause.Running;
        pause.RaiseTick();
        await measure.Measured;
        await DragCorner(core, 0, Drag);
        measure.Release();
        await pause.Running;
        pause.RaiseTick();

        // The first drag's width is stale: the first report is the second drag's final width, and the only one.
        var report = await Next(reports);
        Assert.InRange(report.Width, StartWidth + 2 * Drag - 2, StartWidth + 2 * Drag + 2);
        Assert.Equal(await BoxWidth(core, 0), report.Width);
        Assert.False(reports.Reader.TryRead(out _));
    }

    [WpfFact]
    public async Task Reports_nothing_of_a_page_replaced_right_after_its_drag_was_released()
    {
        var pause = new FakeDragPause();
        var measure = new HeldImageMeasure();
        using var harness = new DescriptionWebViewHarness(pause, measure);
        var reports = Listen(harness);
        var core = await ShowImages(harness, $"![a|{StartWidth}](images/{TaskId}/one.png)");
        await DragCorner(core, 0, Drag);

        // Hold the old measurement until the replacement page's drag has reached the adapter.
        await pause.Running;
        pause.RaiseTick();
        try
        {
            await measure.Measured;
            await ShowNextPage(harness);
            await DragCorner(core, 0, Drag);
            await pause.Running;
        }
        finally
        {
            measure.Release();
        }
        pause.RaiseTick();

        var report = await Next(reports);
        Assert.InRange(report.Width, NextWidth + Drag - 2, NextWidth + Drag + 2);
        Assert.False(reports.Reader.TryRead(out _));
    }

    private const int NextWidth = 300;

    // Shows another page, whose image is another width, so a report of the old page's would show, and returns once it has loaded.
    private static async Task ShowNextPage(DescriptionWebViewHarness harness)
    {
        WriteImage(harness, "next.png");
        harness.Port.Show(DescriptionPage.WithResizableImages(
            $"![b|{NextWidth}](images/{TaskId}/next.png)", dark: false, harness.Port.CloneAddress));
        await harness.NextLoadedPageTitle();
        await harness.NextClonedFileAddress();
        await WaitForBoundPage(harness);
    }

    // The adapter learns of the page's boxes after the page has loaded, and the browser tells of a dragged box's style only once it
    // has. The adapter sets the element's height to the page's own height after that, so the height is the signal that a drag counts.
    // The page's own height is the body's, and the element's is the whole page's: the body's margins make it a little taller.
    private static async Task WaitForBoundPage(DescriptionWebViewHarness harness)
    {
        await harness.WaitForHeight((height, content) => content > 0 && height >= content);
        await harness.WaitForRenderedFrame();
    }

    private const string TaskId = "0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";

    private static void WriteImage(DescriptionWebViewHarness harness, string file)
    {
        var folder = Path.Combine(harness.CloneFolder, "images", TaskId);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, file), Png);
    }

    // Holds the real left mouse button down on a blank part of the page, so the program sees the button as held while the
    // test's drag, which is sent to the browser alone, goes on.
    private static HeldButton HoldButton(DescriptionWebViewHarness harness)
    {
        KeyboardInput.BringToFront(new System.Windows.Interop.WindowInteropHelper(harness.Window).Handle);
        MouseInput.Press(harness.Control.PointToScreen(new System.Windows.Point(harness.Control.ActualWidth - 6, 4)));
        return new HeldButton();
    }

    private sealed class HeldButton : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (!_released)
            {
                _released = true;
                MouseInput.Release();
            }
        }
    }

    private static Channel<ImageResize> Listen(DescriptionWebViewHarness harness)
    {
        var reports = Channel.CreateUnbounded<ImageResize>();
        harness.Port.ImageResized += (_, resize) => reports.Writer.TryWrite(resize);
        return reports;
    }

    // Shows the markdown as a description page whose images are the given clone files, and returns once the browser has
    // loaded them, so the boxes have their final size.
    private static async Task<CoreWebView2> ShowImages(DescriptionWebViewHarness harness, string markdown, params string[] files)
    {
        foreach (var file in files.Length == 0 ? ["one.png"] : files)
        {
            WriteImage(harness, file);
        }

        var core = await harness.Started;
        harness.Port.Show(DescriptionPage.WithResizableImages(markdown, dark: false, harness.Port.CloneAddress));
        await harness.NextLoadedPageTitle();
        for (var loaded = 0; loaded < Math.Max(files.Length, 1); loaded++)
        {
            await harness.NextClonedFileAddress();
        }

        await WaitForBoundPage(harness);
        return core;
    }

    // A user's drag holds the mouse button from the press to the release, so the drag is made with the real button held, which is
    // how the adapter tells a slow step from the end of the drag. The report comes once the button is released.
    private static async Task DragCornerHoldingButton(DescriptionWebViewHarness harness, CoreWebView2 core, int image, int distance)
    {
        using var held = HoldButton(harness);
        await DragCorner(core, image, distance);
    }

    // Presses the resize corner of the image box, drags it right by the distance in steps, and releases.
    private static async Task DragCorner(CoreWebView2 core, int image, int distance)
    {
        var (x, y) = await Corner(core, image);
        await Mouse(core, "mouseMoved", x, y, 0);
        await Mouse(core, "mousePressed", x, y, 1);
        for (var step = 1; step <= 6; step++)
        {
            await Mouse(core, "mouseMoved", x + distance * step / 6.0, y, 1);
        }

        await Mouse(core, "mouseReleased", x + distance, y, 0);
    }

    // Presses the resize corner of the first image box and drags it halfway right, in steps, leaving the button down.
    private static async Task<(double X, double Y)> PressCornerAndDragHalfway(CoreWebView2 core)
    {
        var (x, y) = await Corner(core, 0);
        await Mouse(core, "mouseMoved", x, y, 0);
        await Mouse(core, "mousePressed", x, y, 1);
        for (var step = 1; step <= 3; step++)
        {
            await Mouse(core, "mouseMoved", x + Drag / 2 * step / 3.0, y, 1);
        }

        return (x, y);
    }

    // A point inside the box's bottom-right corner, where the browser draws the resize handle.
    private static async Task<(double X, double Y)> Corner(CoreWebView2 core, int image)
    {
        var json = await core.ExecuteScriptAsync(
            $"(() => {{ const r = document.querySelector('[data-image=\"{image}\"]').getBoundingClientRect(); return [r.right - 4, r.bottom - 4]; }})()");
        var point = JsonSerializer.Deserialize<double[]>(json) ?? throw new InvalidOperationException("The box has no position.");
        return (point[0], point[1]);
    }

    private static async Task<int> BoxWidth(CoreWebView2 core, int image) => int.Parse(
        await core.ExecuteScriptAsync($"document.querySelector('[data-image=\"{image}\"]').offsetWidth"), CultureInfo.InvariantCulture);

    private static Task Mouse(CoreWebView2 core, string type, double x, double y, int buttons) =>
        core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", FormattableString.Invariant(
            $"{{\"type\":\"{type}\",\"x\":{x},\"y\":{y},\"button\":\"{(buttons == 0 && type == "mouseMoved" ? "none" : "left")}\",\"buttons\":{buttons},\"clickCount\":{(type == "mouseMoved" ? 0 : 1)}}}"));
}
