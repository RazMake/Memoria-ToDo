using Memoria.ToDo.Desktop.Updates;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IUpdateLifetime"/>, as Shell sees Updates. It keeps the exit each Start got and counts
/// the Start and StopAsync calls. A test can run a check when Start or StopAsync is called, to see what Shell had done by
/// then, and can make StopAsync fail or wait.
/// </summary>
public sealed class FakeUpdateLifetime : IUpdateLifetime
{
    private readonly List<IAppExit> exits = [];

    /// <summary>The exit each Start call got, in order.</summary>
    public IReadOnlyList<IAppExit> Exits => exits;

    /// <summary>The exit the first Start call got. Shell must have started Updates.</summary>
    /// <exception cref="InvalidOperationException">Start wasn't called.</exception>
    public IAppExit Exit => exits.Count > 0 ? exits[0] : throw new InvalidOperationException("Start wasn't called.");

    /// <summary>How many times Start was called.</summary>
    public int StartCount => exits.Count;

    /// <summary>How many times StopAsync was called.</summary>
    public int StopCount { get; private set; }

    /// <summary>Runs inside each Start call, before it returns. Null when not set.</summary>
    public Action<IAppExit>? OnStart { get; set; }

    /// <summary>Runs when StopAsync is called, and its task is what StopAsync returns. Null when not set: StopAsync completes at once.</summary>
    public Func<Task>? StopOperation { get; set; }

    /// <inheritdoc />
    public void Start(IAppExit exit)
    {
        exits.Add(exit);
        OnStart?.Invoke(exit);
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        StopCount++;
        return StopOperation?.Invoke() ?? Task.CompletedTask;
    }
}
