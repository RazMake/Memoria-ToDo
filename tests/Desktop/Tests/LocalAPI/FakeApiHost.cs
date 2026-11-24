using Memoria.ToDo.Desktop.LocalAPI;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>
/// The fake web server behind the lifetime. A test holds its start or stop with a gate, makes the start fail, and sees whether the
/// stop was told to give up. Each wait only bounds a failure.
/// </summary>
internal sealed class FakeApiHost : IApiHost
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly TaskCompletionSource startCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int starts;
    private int stops;
    private int giveUps;

    /// <summary>The start waits for this before it completes. Completed unless a test sets it.</summary>
    public Task StartGate { get; init; } = Task.CompletedTask;

    /// <summary>The stop waits for this, or for the give-up. Completed unless a test sets it.</summary>
    public Task StopGate { get; init; } = Task.CompletedTask;

    /// <summary>When set, the start throws it, as a host does when the pipe's name is taken.</summary>
    public Exception? StartFailure { get; init; }

    public int StartCount => Volatile.Read(ref starts);

    public int StopCount => Volatile.Read(ref stops);

    /// <summary>How many stops were told to give up.</summary>
    public int GiveUpCount => Volatile.Read(ref giveUps);

    /// <summary>Completes when the host's start was called.</summary>
    public Task StartCalledAsync() => startCalled.Task.WaitAsync(Bound);

    /// <summary>Completes when the host's stop was called.</summary>
    public Task StopCalledAsync() => stopCalled.Task.WaitAsync(Bound);

    /// <inheritdoc />
    public async Task StartAsync()
    {
        Interlocked.Increment(ref starts);
        startCalled.TrySetResult();
        await StartGate;
        if (StartFailure is not null)
            throw StartFailure;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken giveUp)
    {
        Interlocked.Increment(ref stops);
        stopCalled.TrySetResult();
        var gaveUp = new TaskCompletionSource();
        using var registration = giveUp.Register(() => gaveUp.TrySetResult());
        await Task.WhenAny(StopGate, gaveUp.Task);
        if (giveUp.IsCancellationRequested)
        {
            Interlocked.Increment(ref giveUps);
            throw new OperationCanceledException(giveUp);
        }
    }
}
