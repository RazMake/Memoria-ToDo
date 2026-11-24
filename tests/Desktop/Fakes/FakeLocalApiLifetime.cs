using Memoria.ToDo.Desktop.LocalAPI;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="ILocalApiLifetime"/>, as Shell sees the Local API. It counts the Start and StopAsync calls.
/// A test can run a check when Start or StopAsync is called, to see what Shell had done by then, and can make StopAsync fail or wait.
/// </summary>
public sealed class FakeLocalApiLifetime : ILocalApiLifetime
{
    /// <summary>How many times Start was called.</summary>
    public int StartCount { get; private set; }

    /// <summary>How many times StopAsync was called.</summary>
    public int StopCount { get; private set; }

    /// <summary>Runs inside each Start call, before it returns. Null when not set.</summary>
    public Action? OnStart { get; set; }

    /// <summary>Runs when StopAsync is called, and its task is what StopAsync returns. Null when not set: StopAsync completes at once.</summary>
    public Func<Task>? StopOperation { get; set; }

    /// <inheritdoc />
    public void Start()
    {
        StartCount++;
        OnStart?.Invoke();
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        StopCount++;
        return StopOperation?.Invoke() ?? Task.CompletedTask;
    }
}
