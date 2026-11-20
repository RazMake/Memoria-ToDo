using System.Globalization;
using System.Text.Json;
using System.Windows;
using Memoria.ToDo.Desktop.FormattedView;
using Serilog;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// Finds the mention under a point of a description page without a script on the page: DevTools names the node there, and the
/// nearest element around it with data-mention, if any, is read for its number and box. A failure is never thrown: no node at the
/// point is a plain miss, and any other failure is a miss that logs one warning, unless the page was replaced meanwhile, which
/// is a miss too and logs nothing, since the replaced page's nodes are gone.
/// </summary>
/// <param name="isOutdated">Whether the page the lookup began on has been replaced, or the view is gone, since it began.</param>
internal sealed class MentionLookup(IDevToolsCalls devTools, ILogger logger, Func<bool> isOutdated)
{
    // Run by DevTools on a node (this): the number and box of the mention around it, as [number, left, top, width, height], or null.
    private const string MentionOfNode =
        "function() { const m = (this.nodeType === 1 ? this : this.parentElement)?.closest('[data-mention]'); if (!m) { return null; } "
        + "const r = m.getBoundingClientRect(); return [Number(m.dataset.mention), r.left, r.top, r.width, r.height]; }";

    /// <param name="point">The point in WPF's device-independent units, which the page's zoom scales from CSS pixels.</param>
    /// <param name="zoom">The page's zoom factor: a CSS pixel is this many device-independent units.</param>
    /// <returns>The mention around the point with its box in the same units as the point, or null when there is none.</returns>
    public async Task<MentionAt?> FindAsync(Point point, double zoom)
    {
        JsonElement location;
        try
        {
            location = await devTools.CallAsync("DOM.getNodeForLocation", string.Create(
                CultureInfo.InvariantCulture, $"{{\"x\":{(int)Math.Floor(point.X / zoom)},\"y\":{(int)Math.Floor(point.Y / zoom)}}}"));
        }
        catch (Exception)
        {
            // DevTools fails when no node is at the point, which is an ordinary place to hover.
            return null;
        }

        string? objectId = null;
        try
        {
            var node = await devTools.CallAsync("DOM.resolveNode", $"{{\"backendNodeId\":{location.GetProperty("backendNodeId").GetInt32()}}}");
            objectId = node.GetProperty("object").GetProperty("objectId").GetString();
            var mention = await devTools.CallAsync(
                "Runtime.callFunctionOn", $$"""{"objectId":"{{objectId}}","functionDeclaration":"{{MentionOfNode}}","returnByValue":true}""");
            if (!mention.GetProperty("result").TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var found = value.EnumerateArray().Select(number => number.GetDouble()).ToArray();
            return new MentionAt((int)found[0], new Rect(found[1] * zoom, found[2] * zoom, found[3] * zoom, found[4] * zoom));
        }
        catch (Exception failure) when (!isOutdated())
        {
            logger.Warning(failure, "Finding the mention under the pointer failed");
            return null;
        }
        catch (Exception)
        {
            // The page was replaced meanwhile: its nodes are gone, which is no failure.
            return null;
        }
        finally
        {
            if (objectId is not null)
            {
                await ReleaseAsync(objectId);
            }
        }
    }

    // The page keeps a remote object until it is released. A failure here leaves nothing to act on.
    private async Task ReleaseAsync(string objectId)
    {
        try
        {
            await devTools.CallAsync("Runtime.releaseObject", $$"""{"objectId":"{{objectId}}"}""");
        }
        catch (Exception)
        {
        }
    }
}
