using Memoria.ToDo.Desktop.KeepAwake;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IKeepAwakeLifetime"/>, as Shell sees Keep awake. It counts the Start and Stop calls and
/// keeps the thread each Start call ran on. A test can run a check when Start or Stop is called, to see what Shell had done
/// by then, and can make Stop fail by throwing from <see cref="OnStop"/>.
/// </summary>
public sealed class FakeKeepAwakeLifetime : IKeepAwakeLifetime
{
    private readonly List<int> startThreads = [];

    /// <summary>The managed thread each Start call ran on, in order.</summary>
    public IReadOnlyList<int> StartThreads => startThreads;

    /// <summary>How many times Start was called.</summary>
    public int StartCount => startThreads.Count;

    /// <summary>How many times Stop was called.</summary>
    public int StopCount { get; private set; }

    /// <summary>Runs inside each Start call, before it returns. Null when not set.</summary>
    public Action? OnStart { get; set; }

    /// <summary>Runs inside each Stop call, after it is counted, and may throw. Null when not set.</summary>
    public Action? OnStop { get; set; }

    /// <inheritdoc />
    public void Start()
    {
        startThreads.Add(Environment.CurrentManagedThreadId);
        OnStart?.Invoke();
    }

    /// <inheritdoc />
    public void Stop()
    {
        StopCount++;
        OnStop?.Invoke();
    }
}
