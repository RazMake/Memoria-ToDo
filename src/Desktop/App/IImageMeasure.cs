using System.Globalization;
using Memoria.ToDo.Desktop.FormattedView;
using Microsoft.Web.WebView2.Core;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Reads an image box's number and width from the browser, in round trips that take time. Used on the UI thread.</summary>
internal interface IImageMeasure
{
    /// <summary>The image number and the width in pixels of the box that DevTools knows as <paramref name="node"/>.</summary>
    Task<ImageResize> MeasureAsync(CoreWebView2 core, int node);
}

/// <summary>Measures with the DevTools protocol: the box's <c>data-image</c> attribute and its box model.</summary>
internal sealed class DevToolsImageMeasure : IImageMeasure
{
    public async Task<ImageResize> MeasureAsync(CoreWebView2 core, int node)
    {
        var attributes = await DevTools.CallAsync(core, "DOM.getAttributes", $"{{\"nodeId\":{node}}}");
        var box = await DevTools.CallAsync(core, "DOM.getBoxModel", $"{{\"nodeId\":{node}}}");
        var values = attributes.GetProperty("attributes").EnumerateArray().Select(a => a.GetString()).ToArray();
        var image = int.Parse(values[Array.IndexOf(values, "data-image") + 1]!, CultureInfo.InvariantCulture);
        var width = (int)Math.Round(box.GetProperty("model").GetProperty("width").GetDouble());
        return new ImageResize(image, Math.Max(1, width));
    }
}
