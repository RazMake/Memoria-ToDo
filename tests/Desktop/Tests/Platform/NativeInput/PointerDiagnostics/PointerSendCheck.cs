namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The rules a move's point must meet right before it is sent, whether it is the step away or the target.</summary>
internal static class PointerSendCheck
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/>, naming <paramref name="move"/> and the point, unless all hold:
    /// the point is on screen; the window at it (<paramref name="hit"/>) is <paramref name="target"/> (the element's own window)
    /// or a child of it; and the foreground window is <paramref name="owner"/> (the window the caller passed, which differs
    /// from the target for an element in a popup).
    /// </summary>
    public static void Verify(string move, Win32.Point point, bool onScreen, nint target, nint hit, Func<nint, nint, bool> isChildOf, nint owner, nint foreground)
    {
        ArgumentNullException.ThrowIfNull(isChildOf);
        if (!onScreen)
        {
            throw new InvalidOperationException($"{move}: the point ({point.X}, {point.Y}) is off screen, so nothing was sent.");
        }

        if (hit != target && !isChildOf(hit, target))
        {
            throw new InvalidOperationException(
                $"{move}: another window covers the point ({point.X}, {point.Y}) and would get the move; the window there is {hit:X}, "
                + $"not the target window {target:X} or a child of it, so nothing was sent.");
        }

        if (foreground != owner)
        {
            throw new InvalidOperationException(
                $"{move}: the window in front is {foreground:X}, not the caller's window {owner:X}; a hover needs the caller's window to be in front, so nothing was sent.");
        }
    }
}