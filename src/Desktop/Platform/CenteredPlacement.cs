using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Where a window goes to be centered on a screen's work area, in device pixels.</summary>
internal static class CenteredPlacement
{
    /// <param name="workPixels">The screen's work area in device pixels, in virtual-screen coordinates.</param>
    /// <param name="scale">The screen's scale: 1.5 is 150%.</param>
    /// <param name="size">The window's size in WPF units.</param>
    /// <returns>The window's top-left in pixels. A window bigger than the work area on an axis starts at the work area's edge on that axis.</returns>
    public static (int Left, int Top) TopLeft(Rect workPixels, double scale, Size size) =>
        (Axis(workPixels.Left, workPixels.Width, size.Width * scale), Axis(workPixels.Top, workPixels.Height, size.Height * scale));

    private static int Axis(double start, double available, double needed) =>
        (int)Math.Round(start + Math.Max(0, (available - needed) / 2), MidpointRounding.AwayFromZero);
}
