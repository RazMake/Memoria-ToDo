namespace Memoria.ToDo.Desktop.Platform;

/// <summary>When a press that moves has gone far enough to be a drag.</summary>
internal static class DragDistance
{
    /// <summary>Whether the move reaches the system's drag distance in either direction.</summary>
    public static bool Passed(double dx, double dy, double minimumX, double minimumY) =>
        Math.Abs(dx) >= minimumX || Math.Abs(dy) >= minimumY;
}
