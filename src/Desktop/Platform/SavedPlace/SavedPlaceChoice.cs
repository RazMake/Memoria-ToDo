using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Whether a saved place is on a screen now, and the size its window opens at.</summary>
internal static class SavedPlaceChoice
{
    /// <summary>
    /// The saved place with its size raised to <paramref name="minimumPixels"/> and cut to the work area of the screen that holds
    /// at least half of the window by area; the position is never changed. Null when no screen holds half, or when the window at
    /// the size it opens at no longer has half in that screen's work area, so the caller opens the window as Show on the pointer's
    /// screen does. The work area wins when the minimum is larger than it.
    /// </summary>
    internal static SavedPlace? Choose(SavedPlace saved, IReadOnlyList<Rect> workAreas, Size minimumPixels)
    {
        var window = Bounds(saved);
        var best = default(Rect);
        var bestOverlap = 0.0;
        foreach (var work in workAreas)
        {
            var area = OverlapArea(window, work);
            if (area > bestOverlap)
            {
                best = work;
                bestOverlap = area;
            }
        }

        if (HasLessThanHalf(bestOverlap, window))
        {
            return null;
        }

        var opened = saved with
        {
            Width = (int)Math.Min(Math.Max(saved.Width, Math.Ceiling(minimumPixels.Width)), best.Width),
            Height = (int)Math.Min(Math.Max(saved.Height, Math.Ceiling(minimumPixels.Height)), best.Height),
        };
        var openedWindow = Bounds(opened);
        return HasLessThanHalf(OverlapArea(openedWindow, best), openedWindow) ? null : opened;
    }

    private static Rect Bounds(SavedPlace place) => new(place.Left, place.Top, place.Width, place.Height);

    private static double OverlapArea(Rect window, Rect work)
    {
        var overlap = Rect.Intersect(window, work);
        return overlap.IsEmpty ? 0 : overlap.Width * overlap.Height;
    }

    private static bool HasLessThanHalf(double overlapArea, Rect window) => overlapArea * 2 < window.Width * window.Height;
}
