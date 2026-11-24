namespace Memoria.ToDo.Desktop.Platform;

/// <summary>When a list scrolls during a drag, and how fast: while the pointer is in a band along its top or bottom edge.</summary>
internal static class EdgeBand
{
    /// <summary>How far from the top or bottom edge the band reaches, in device-independent pixels.</summary>
    public const double Size = 40;

    /// <summary>The most the list scrolls in one step, which the pointer at the very edge reaches.</summary>
    public const double MaxStep = 24;

    /// <summary>
    /// How far to scroll for a pointer at <paramref name="y"/> in a list <paramref name="height"/> high: negative to scroll up,
    /// positive to scroll down, and 0 outside the bands. The step grows toward the edge.
    /// </summary>
    public static double Step(double y, double height)
    {
        if (y < Size)
        {
            return -MaxStep * Math.Min(1, (Size - y) / Size);
        }

        return y > height - Size ? MaxStep * Math.Min(1, (y - (height - Size)) / Size) : 0;
    }
}
