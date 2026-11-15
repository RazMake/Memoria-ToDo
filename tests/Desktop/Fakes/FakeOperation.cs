namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// One kind of call a fake port answers: each call succeeds with the result, fails, or is held until the test
/// releases it or the call is cancelled. Shared by the fakes whose calls a test must control. A "completed" signal fires
/// when the call's task completes, which can be just before the caller reacts to it.
/// </summary>
internal sealed class FakeOperation<T>
{
    private enum Mode { Succeed, Fail, Hold }

    private readonly object gate = new();
    private readonly List<TaskCompletionSource<T>> held = [];
    private readonly CountSignal started = new();
    private readonly CountSignal completed = new();
    private readonly CountSignal cancelled = new();
    private Mode mode = Mode.Succeed;
    private Exception failure = new InvalidOperationException("The call failed.");

    /// <summary>What a successful call returns.</summary>
    public T? Result { get; set; }

    public int Count => started.Count;

    public int CompletedCount => completed.Count;

    public void Succeed()
    {
        lock (gate) { mode = Mode.Succeed; }
    }

    public void Fail(Exception exception)
    {
        lock (gate) { mode = Mode.Fail; failure = exception; }
    }

    public void Hold()
    {
        lock (gate) { mode = Mode.Hold; }
    }

    /// <summary>Completes every held call successfully with the current <see cref="Result"/>. Later calls still wait while held.</summary>
    public void Release()
    {
        TaskCompletionSource<T>[] toRelease;
        lock (gate)
        {
            toRelease = [.. held];
            held.Clear();
        }

        foreach (var call in toRelease)
        {
            if (call.TrySetResult(Result!))
            {
                completed.Increment();
            }
        }
    }

    /// <summary>Completes every held call with the failure.</summary>
    public void ReleaseWithFailure(Exception exception)
    {
        TaskCompletionSource<T>[] toRelease;
        lock (gate)
        {
            toRelease = [.. held];
            held.Clear();
        }

        foreach (var call in toRelease)
        {
            if (call.TrySetException(exception))
            {
                completed.Increment();
            }
        }
    }

    public Task WaitForCallsAsync(int count) => started.WaitForAsync(count);

    public Task WaitForCompletedAsync(int count) => completed.WaitForAsync(count);

    public Task WaitForCancellationAsync() => cancelled.WaitForAsync(1);

    public Task<T> Call(CancellationToken cancellationToken)
    {
        Mode current;
        Exception currentFailure;
        T? result;
        TaskCompletionSource<T>? call = null;
        lock (gate)
        {
            current = mode;
            currentFailure = failure;
            result = Result;
            started.Increment();
            if (current == Mode.Hold && !cancellationToken.IsCancellationRequested)
            {
                call = new TaskCompletionSource<T>();
                held.Add(call);
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            cancelled.Increment();
            completed.Increment();
            return Task.FromCanceled<T>(cancellationToken);
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
        return current == Mode.Fail ? Task.FromException<T>(currentFailure) : Task.FromResult(result!);
    }
}
