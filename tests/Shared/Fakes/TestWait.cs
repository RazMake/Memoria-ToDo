namespace Memoria.ToDo.Shared.Fakes;

/// <summary>How long a unit test waits for a signal before it fails, so a lost signal fails one test instead of hanging the run.</summary>
public static class TestWait
{
    /// <summary>The longest a unit test or fake waits for a signal, such as a started pass or a completed task.</summary>
    public static readonly TimeSpan ForSignal = TimeSpan.FromSeconds(3);
}
