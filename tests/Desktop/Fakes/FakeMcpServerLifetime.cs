using Memoria.ToDo.Desktop.MCPServer;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IMcpServerLifetime"/>, as Shell sees the MCP server. It counts the Start and StopAsync
/// calls and keeps the thread each Start call ran on. A test can run a check when Start or StopAsync is called, to see
/// what Shell had done by then, and can make Start or StopAsync fail or StopAsync wait. <see cref="Started"/> completes
/// at the first Start call, for a test that waits for it.
/// </summary>
public sealed class FakeMcpServerLifetime : IMcpServerLifetime
{
    private readonly List<int> startThreads = [];
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when Start is first called.</summary>
    public Task Started => started.Task;

    /// <summary>The managed thread each Start call ran on, in order.</summary>
    public IReadOnlyList<int> StartThreads => startThreads;

    /// <summary>How many times Start was called.</summary>
    public int StartCount => startThreads.Count;

    /// <summary>How many times StopAsync was called.</summary>
    public int StopCount { get; private set; }

    /// <summary>Runs inside each Start call, before it returns. Null when not set.</summary>
    public Action? OnStart { get; set; }

    /// <summary>Runs when StopAsync is called, and its task is what StopAsync returns. Null when not set: StopAsync completes at once.</summary>
    public Func<Task>? StopOperation { get; set; }

    /// <inheritdoc />
    public void Start()
    {
        startThreads.Add(Environment.CurrentManagedThreadId);
        started.TrySetResult();
        OnStart?.Invoke();
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        StopCount++;
        return StopOperation?.Invoke() ?? Task.CompletedTask;
    }
}
