namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Runs one operation at a time, and stops for good. <see cref="StopAsync"/> cancels the running operation and
/// completes only after it, its transport cleanup, and the owned worker have ended.
/// </summary>
internal sealed class Operations(OwnedWorker worker)
{
    private readonly object stopGate = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private Task? stopping;

    public bool IsStopped => shutdown.IsCancellationRequested;

    /// <summary>Runs <paramref name="work"/> alone. Throws <see cref="ObjectDisposedException"/> once stopping has begun.</summary>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken caller)
    {
        ObjectDisposedException.ThrowIf(IsStopped, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, caller);
        try
        {
            await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (IsStoppedNotCancelledBy(caller))
        {
            throw new ObjectDisposedException(nameof(Operations));
        }

        try
        {
            linked.Token.ThrowIfCancellationRequested();
            return await work(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (IsStoppedNotCancelledBy(caller))
        {
            throw new ObjectDisposedException(nameof(Operations));
        }
        finally
        {
            gate.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (stopGate)
        {
            stopping ??= ShutDownAsync();
        }

        return stopping.WaitAsync(cancellationToken);
    }

    private bool IsStoppedNotCancelledBy(CancellationToken caller) => IsStopped && !caller.IsCancellationRequested;

    private async Task ShutDownAsync()
    {
        await shutdown.CancelAsync().ConfigureAwait(false);
        // Holding the gate for good proves the running operation, its cleanup, and its key disposal have ended.
        await gate.WaitAsync().ConfigureAwait(false);
        worker.Complete();
        await worker.Completion.ConfigureAwait(false);
        // Nothing can run any more: later callers fail on IsStopped, and repeated stops share this task.
        gate.Dispose();
        shutdown.Dispose();
    }
}
