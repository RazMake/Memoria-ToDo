using Memoria.ToDo.Desktop.FormattedView;
using Microsoft.Web.WebView2.Core;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The real width measurement, whose result is held back until the test lets it go, so a test knows the browser has been read
/// and can then act before the web view gets the result, with no sleeps. Used on the UI thread, as the web view uses it.
/// </summary>
internal sealed class HeldImageMeasure : IImageMeasure
{
    private readonly DevToolsImageMeasure _real = new();
    private readonly TaskCompletionSource<ImageResize> _measured = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes once the browser has been read, while the web view still waits for the result.</summary>
    public Task<ImageResize> Measured => _measured.Task.WaitAsync(DescriptionWebViewHarness.Limit);

    /// <summary>Lets the web view have the result.</summary>
    public void Release() => _released.SetResult();

    public async Task<ImageResize> MeasureAsync(CoreWebView2 core, int node)
    {
        var resize = await _real.MeasureAsync(core, node);
        _measured.TrySetResult(resize);
        await _released.Task;
        return resize;
    }
}
