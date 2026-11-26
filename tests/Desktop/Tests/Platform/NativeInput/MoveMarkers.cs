namespace Memoria.ToDo.Desktop.Platform.Tests;

internal sealed class MoveMarkers
{
    private long _next;

    public static MoveMarkers Shared { get; } = new(RandomStart());

    public MoveMarkers(nint first)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(first);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(first, (nint)int.MaxValue);
        _next = first;
    }

    public nint Next()
    {
        while (true)
        {
            var current = Volatile.Read(ref _next);
            if (current > int.MaxValue)
            {
                throw new InvalidOperationException("The pointer move markers of this process ran out.");
            }

            if (Interlocked.CompareExchange(ref _next, current + 1, current) == current)
            {
                return (nint)current;
            }
        }
    }

    // Leaves room to count up from the randomized process starting value.
    private static nint RandomStart() => (nint)Random.Shared.NextInt64(1, 1L << 30);
}
