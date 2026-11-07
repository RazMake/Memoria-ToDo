namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// A counter a test can wait on: <see cref="WaitForAsync"/> completes once the count reaches a number.
/// The timeout only bounds a failing test; a passing test never waits for it.
/// </summary>
internal sealed class CountSignal
{
    private static readonly TimeSpan FailAfter = TimeSpan.FromSeconds(10);

    private readonly object gate = new();
    private readonly List<(int Count, TaskCompletionSource Done)> waiters = [];
    private int count;

    public int Count
    {
        get { lock (gate) { return count; } }
    }

    public void Increment()
    {
        List<TaskCompletionSource> ready = [];
        lock (gate)
        {
            count++;
            for (var i = waiters.Count - 1; i >= 0; i--)
            {
                if (waiters[i].Count <= count)
                {
                    ready.Add(waiters[i].Done);
                    waiters.RemoveAt(i);
                }
            }
        }

        foreach (var done in ready)
        {
            done.TrySetResult();
        }
    }

    /// <summary>Completes when the count is at least <paramref name="atLeast"/>; throws <see cref="TimeoutException"/> if that takes too long.</summary>
    public Task WaitForAsync(int atLeast)
    {
        TaskCompletionSource done;
        lock (gate)
        {
            if (count >= atLeast)
            {
                return Task.CompletedTask;
            }

            done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Add((atLeast, done));
        }

        return done.Task.WaitAsync(FailAfter);
    }
}
