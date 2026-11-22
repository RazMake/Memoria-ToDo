namespace Memoria.ToDo.Desktop.Platform.Tests;

internal static class AbsolutePointerCoordinates
{
    public static (int X, int Y) Normalize(Win32.Point point, Win32.Rect virtualScreen)
    {
        if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualScreen), "The virtual screen has no area.");
        }

        if (point.X < virtualScreen.Left || point.X >= virtualScreen.Right
            || point.Y < virtualScreen.Top || point.Y >= virtualScreen.Bottom)
        {
            throw new ArgumentOutOfRangeException(nameof(point), $"({point.X}, {point.Y}) is outside the virtual screen.");
        }

        return (Scale(point.X - virtualScreen.Left, virtualScreen.Width), Scale(point.Y - virtualScreen.Top, virtualScreen.Height));
    }

    private static int Scale(int offset, int extent) =>
        extent == 1 ? 0 : (int)(((long)offset * 65535 + (extent - 1) / 2) / (extent - 1));
}
