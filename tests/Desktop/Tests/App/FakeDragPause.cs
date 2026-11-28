namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The pause of a drag, driven by the test: it ticks only when <see cref="RaiseTick"/> says, and only while the web view has it
/// running, as a timer would. The mouse button reads as up unless the test sets <see cref="ButtonDown"/>.
/// Used on the UI thread, as the web view uses it.
/// </summary>
internal sealed class FakeDragPause : IDragPause
{
    private TaskCompletionSource _running = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event EventHandler? Tick;

    /// <summary>
    /// Completes once the web view has the pause running, which is how a test knows a drag step reached it: the browser tells of
    /// a step some time after the mouse event is sent. Already complete while it runs.
    /// </summary>
    public Task Running => _running.Task.WaitAsync(DescriptionWebViewHarness.Limit);

    /// <summary>Whether the mouse button reads as held.</summary>
    public bool ButtonDown { get; set; }

    public bool IsButtonDown => ButtonDown;

    public void Start() => _running.TrySetResult();

    public void Stop()
    {
        if (_running.Task.IsCompleted)
        {
            _running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Makes the pause pass now, which does nothing when no pause is running, as for a stopped timer.</summary>
    public void RaiseTick()
    {
        if (_running.Task.IsCompleted)
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}
