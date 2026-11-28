using System.Collections.Concurrent;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// One dedicated background thread that runs synchronous work (file I/O and native Git calls) in order.
/// Callers await the returned task; nothing ever blocks on it.
/// </summary>
internal sealed class OwnedWorker
{
    private readonly BlockingCollection<Action> queue = new(1);
    private readonly TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object gate = new();
    private bool started;
    private bool completed;

    /// <summary>Completes after the worker has run everything queued before <see cref="Complete"/>.</summary>
    public Task Completion => done.Task;

    public Task<T> Run<T>(Func<T> work)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            if (completed)
            {
                result.SetException(new ObjectDisposedException(nameof(OwnedWorker)));
                return result.Task;
            }

            if (!started)
            {
                started = true;
                new Thread(Loop) { IsBackground = true, Name = "Memoria sync worker" }.Start();
            }

            // The coordinator awaits each item before dispatching the next, so the single slot is always free; a full slot is a bug.
            if (!queue.TryAdd(() => Execute(work, result)))
                throw new InvalidOperationException("The sync worker accepts one work item at a time.");
        }

        return result.Task;
    }

    public Task Run(Action work) => Run(() =>
    {
        work();
        return true;
    });

    /// <summary>Refuses further work; queued work still runs.</summary>
    public void Complete()
    {
        lock (gate)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            queue.CompleteAdding();
            if (!started)
            {
                done.TrySetResult();
            }
        }
    }

    private static void Execute<T>(Func<T> work, TaskCompletionSource<T> result)
    {
        try
        {
            result.SetResult(work());
        }
        catch (Exception ex)
        {
            // Faulted, not canceled, so awaiting rethrows the original exception, including a cancellation nobody requested.
            result.SetException(ex);
        }
    }

    private void Loop()
    {
        foreach (var work in queue.GetConsumingEnumerable())
        {
            work();
        }

        done.TrySetResult();
    }
}
