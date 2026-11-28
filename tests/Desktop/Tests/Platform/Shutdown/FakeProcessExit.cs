using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.Platform.Tests;

internal sealed class FakeProcessExit : IProcessExit
{
    private readonly TaskCompletionSource<int> requested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<int> terminated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int requests;
    private int terminations;

    public Action? OnRequest { get; set; }
    public int RequestCount => Volatile.Read(ref requests);
    public int TerminationCount => Volatile.Read(ref terminations);
    public Task<int> Requested => requested.Task.WaitAsync(TestWait.ForSignal);
    public Task<int> Terminated => terminated.Task.WaitAsync(TestWait.ForSignal);
    public Task Finished => finished.Task.WaitAsync(TestWait.ForSignal);

    public void RequestExit(int exitCode)
    {
        Interlocked.Increment(ref requests);
        requested.TrySetResult(exitCode);
        try
        {
            OnRequest?.Invoke();
        }
        finally
        {
            finished.TrySetResult();
        }
    }

    public void Terminate(int exitCode)
    {
        Interlocked.Increment(ref terminations);
        terminated.TrySetResult(exitCode);
    }
}
