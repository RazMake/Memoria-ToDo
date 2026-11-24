using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IStartWithWindows"/>: it keeps the setting in memory, remembers every Set,
/// and can be told to fail, as when the app isn't installed or Windows refuses the write.
/// </summary>
public sealed class FakeStartWithWindows : IStartWithWindows
{
    private readonly List<bool> setCalls = [];
    private readonly List<int> callThreads = [];

    /// <summary>Whether the setting is on now.</summary>
    public bool On { get; set; }

    /// <summary>The managed id of the thread each <see cref="IsOn"/> and <see cref="Set"/> call ran on, in order, for tests of callers that must use the UI thread.</summary>
    public IReadOnlyList<int> CallThreads
    {
        get { lock (callThreads) { return [.. callThreads]; } }
    }

    /// <summary>The values Set was called with, in order, including those that failed.</summary>
    public IReadOnlyList<bool> SetCalls => setCalls;

    /// <summary>When set, <see cref="Set"/> throws it and the setting stays as it was.</summary>
    public Exception? SetFailure { get; set; }

    /// <inheritdoc />
    public bool IsOn()
    {
        RecordThread();
        return On;
    }

    /// <inheritdoc />
    public void Set(bool on)
    {
        RecordThread();
        setCalls.Add(on);
        if (SetFailure is not null)
        {
            throw SetFailure;
        }

        On = on;
    }

    private void RecordThread()
    {
        lock (callThreads) { callThreads.Add(Environment.CurrentManagedThreadId); }
    }
}
