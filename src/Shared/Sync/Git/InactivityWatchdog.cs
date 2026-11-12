namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Runs an action when a watched network wait gets no progress for the whole limit. Each <see cref="Begin"/> restarts
/// the clock, and <see cref="End"/> stops it, so time spent outside a read, write or wait never counts.
/// </summary>
internal sealed class InactivityWatchdog : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly ITimer timer;
    private readonly TimeSpan limit;
    private readonly Action onExpired;
    private readonly TaskCompletionSource expiry = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool expired;
    private bool disposed;

    /// <param name="onExpired">Runs once on the timer's thread, outside this watchdog's lock. It must not throw.</param>
    public InactivityWatchdog(TimeProvider time, TimeSpan limit, Action onExpired)
    {
        this.limit = limit;
        this.onExpired = onExpired;
        timer = time.CreateTimer(static state => ((InactivityWatchdog)state!).Expire(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>True once the limit passed without progress; it stays true.</summary>
    public bool Expired
    {
        get
        {
            lock (gate)
            {
                return expired;
            }
        }
    }

    /// <summary>Completes when the limit passed without progress, so a waiter can stop waiting for something that never ends.</summary>
    public Task ExpiredTask => expiry.Task;

    /// <summary>Starts or restarts the clock for one wait on the network.</summary>
    public void Begin() => Change(limit);

    /// <summary>Stops the clock; the wait ended.</summary>
    public void End() => Change(Timeout.InfiniteTimeSpan);

    /// <summary>Stops the clock for good, and completes after any running expiry action has finished.</summary>
    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed = true;
        }

        return timer.DisposeAsync();
    }

    private void Change(TimeSpan due)
    {
        lock (gate)
        {
            if (!disposed && !expired)
            {
                timer.Change(due, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Expire()
    {
        lock (gate)
        {
            if (disposed || expired)
            {
                return;
            }

            expired = true;
        }

        // Outside the lock: the action may wait for a reader that needs the lock to stop its clock.
        try
        {
            onExpired();
        }
        finally
        {
            expiry.TrySetResult();
        }
    }
}
