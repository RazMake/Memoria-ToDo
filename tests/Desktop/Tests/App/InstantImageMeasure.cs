using Memoria.ToDo.Desktop.FormattedView;
using Microsoft.Web.WebView2.Core;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// A width measurement that answers at once with the result the test gives, whatever the box. The web view then reports in the same
/// turn as the pause that asked for it, so a test sees the report, or its absence, with no waiting.
/// </summary>
internal sealed class InstantImageMeasure : IImageMeasure
{
    private readonly ImageResize _result;

    public InstantImageMeasure(ImageResize result) => _result = result;

    public Task<ImageResize> MeasureAsync(CoreWebView2 core, int node) => Task.FromResult(_result);
}
