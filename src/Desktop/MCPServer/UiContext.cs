namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>
/// The UI thread, as <see cref="Capture"/> found it. Work that Updates and Start with Windows expect on the UI thread, and the
/// <see cref="IMcpServerStatus.Changed"/> event, run there. Before <see cref="Capture"/>, or when the capturing thread had no
/// synchronization context, everything runs on the calling thread.
/// </summary>
internal sealed class UiContext
{
    private SynchronizationContext? _context;
    private int _threadId;

    /// <summary>Remembers the calling thread as the UI thread. Called on it.</summary>
    public void Capture()
    {
        _threadId = Environment.CurrentManagedThreadId;
        _context = SynchronizationContext.Current;
    }

    /// <summary>Runs the work on the UI thread, at once when already there, and returns what it returned or threw.</summary>
    public Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken)
    {
        if (_context is not { } context || Environment.CurrentManagedThreadId == _threadId)
        {
            return Task.FromResult(work());
        }

        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(_ =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                done.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                done.TrySetResult(work());
            }
            catch (Exception failure)
            {
                done.TrySetException(failure);
            }
        }, null);
        return done.Task.WaitAsync(cancellationToken);
    }

    /// <summary>Runs the action on the UI thread without waiting, at once when already there.</summary>
    public void Post(Action action)
    {
        if (_context is not { } context || Environment.CurrentManagedThreadId == _threadId)
        {
            action();
            return;
        }

        context.Post(_ => action(), null);
    }
}
