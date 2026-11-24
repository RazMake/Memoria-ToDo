namespace Memoria.ToDo.Desktop.McpServer;

/// <summary>
/// Counts work in progress, such as the tool calls or the connections, so a stop can refuse new work and wait for what is running.
/// Safe to call from any thread.
/// </summary>
internal sealed class ActiveWork
{
    private readonly object _gate = new();
    private int _active;
    private bool _closed;
    private TaskCompletionSource? _idle;

    /// <summary>Counts one more piece of work. False once <see cref="CloseAsync"/> was called, and then nothing is counted.</summary>
    public bool TryEnter()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return false;
            }

            _active++;
            return true;
        }
    }

    /// <summary>Ends one piece of work that <see cref="TryEnter"/> counted.</summary>
    public void Exit()
    {
        TaskCompletionSource? idle = null;
        lock (_gate)
        {
            if (--_active == 0)
            {
                idle = _idle;
                _idle = null;
            }
        }

        idle?.TrySetResult();
    }

    /// <summary>Refuses new work from now on, and completes when the work counted so far has ended.</summary>
    public Task CloseAsync()
    {
        lock (_gate)
        {
            _closed = true;
            return WhenIdleLocked();
        }
    }

    private Task WhenIdleLocked()
    {
        if (_active == 0)
        {
            return Task.CompletedTask;
        }

        _idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _idle.Task;
    }
}
