using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IAppUpdates"/>. Each call succeeds, fails, or is held until the test releases it
/// or the call is cancelled. Tests wait on the signals, never on delays. A "completed" signal fires when the call's
/// task completes, which can be just before the caller reacts to it.
/// </summary>
public sealed class FakeAppUpdates : IAppUpdates
{
    private enum Mode { Succeed, Fail, Hold }

    private readonly object gate = new();
    private readonly List<TaskCompletionSource> held = [];
    private readonly CountSignal started = new();
    private readonly CountSignal completed = new();
    private readonly CountSignal cancelled = new();
    private Mode mode = Mode.Succeed;
    private Exception failure = new InvalidOperationException("The update check failed.");

    /// <summary>How many calls began.</summary>
    public int CallCount => started.Count;

    /// <summary>How many calls ended, whether they succeeded, failed, or were cancelled.</summary>
    public int CompletedCallCount => completed.Count;

    /// <summary>From now on, calls succeed at once. This is the starting behavior.</summary>
    public void Succeed()
    {
        lock (gate) { mode = Mode.Succeed; }
    }

    /// <summary>From now on, calls fail with this exception.</summary>
    public void Fail(Exception exception)
    {
        lock (gate) { mode = Mode.Fail; failure = exception; }
    }

    /// <summary>From now on, calls wait until <see cref="Release"/> or until their token is cancelled.</summary>
    public void Hold()
    {
        lock (gate) { mode = Mode.Hold; }
    }

    /// <summary>Completes every held call successfully. Later calls still wait while held.</summary>
    public void Release()
    {
        TaskCompletionSource[] toRelease;
        lock (gate)
        {
            toRelease = [.. held];
            held.Clear();
        }

        foreach (var call in toRelease)
        {
            if (call.TrySetResult())
            {
                completed.Increment();
            }
        }
    }

    /// <summary>Completes when at least this many calls have begun.</summary>
    public Task WaitForCallsAsync(int count) => started.WaitForAsync(count);

    /// <summary>Completes when at least this many calls have ended.</summary>
    public Task WaitForCompletedCallsAsync(int count) => completed.WaitForAsync(count);

    /// <summary>Completes when a call has seen its token cancelled.</summary>
    public Task WaitForCancellationAsync() => cancelled.WaitForAsync(1);

    /// <inheritdoc />
    public Task DownloadNewerVersionAsync(CancellationToken cancellationToken)
    {
        Mode current;
        Exception currentFailure;
        TaskCompletionSource? call = null;
        lock (gate)
        {
            current = mode;
            currentFailure = failure;
            started.Increment();
            if (current == Mode.Hold && !cancellationToken.IsCancellationRequested)
            {
                call = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                held.Add(call);
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            cancelled.Increment();
            completed.Increment();
            return Task.FromCanceled(cancellationToken);
        }

        if (call is not null)
        {
            cancellationToken.Register(() =>
            {
                lock (gate) { held.Remove(call); }
                cancelled.Increment();
                if (call.TrySetCanceled(cancellationToken))
                {
                    completed.Increment();
                }
            });
            return call.Task;
        }

        completed.Increment();
        return current == Mode.Fail ? Task.FromException(currentFailure) : Task.CompletedTask;
    }
}
