using Memoria.ToDo.Desktop.Logs;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="ILogsWindow"/>, for Shell's tests: counts Show calls and adds "Logs" to the shared log
/// for each, when given one, so a test can read the order of the calls, like <see cref="FakeAddWindow"/>.
/// </summary>
public sealed class FakeLogsWindow(List<string>? shownLog = null) : ILogsWindow
{
    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <summary>The managed id of the thread of each Show call, in order.</summary>
    public List<int> ShownOnThreads { get; } = [];

    /// <inheritdoc />
    public void Show()
    {
        ShowCount++;
        ShownOnThreads.Add(Environment.CurrentManagedThreadId);
        shownLog?.Add("Logs");
    }
}
