using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IAddWindow"/>: counts Show calls, and adds "Add" to the shared log, when given one,
/// so a test can read the order in which windows were shown.
/// </summary>
public sealed class FakeAddWindow(List<string>? shownLog = null) : IAddWindow
{
    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <summary>What IsOpen answers; false when not set.</summary>
    public bool IsOpen { get; set; }

    /// <inheritdoc />
    public void Show()
    {
        ShowCount++;
        shownLog?.Add("Add");
    }
}

/// <summary>The shared fake of <see cref="IViewTasksWindow"/>: counts Show calls, and logs "View Tasks" like <see cref="FakeAddWindow"/>.</summary>
public sealed class FakeViewTasksWindow(List<string>? shownLog = null) : IViewTasksWindow
{
    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <summary>What IsOpen answers; false when not set.</summary>
    public bool IsOpen { get; set; }

    /// <summary>The managed id of the thread of each Show call, in order.</summary>
    public List<int> ShownOnThreads { get; } = [];

    /// <inheritdoc />
    public void Show()
    {
        ShowCount++;
        ShownOnThreads.Add(Environment.CurrentManagedThreadId);
        shownLog?.Add("View Tasks");
    }
}

/// <summary>
/// The shared fake of <see cref="IConfigureWindow"/>: counts Show calls (and logs "Configure" like
/// <see cref="FakeAddWindow"/>), keeps the two shortcut actions Shell connects, and answers
/// <see cref="ConnectAnswer"/>. Unlike Configure, it registers nothing at once: a test calls the kept actions as
/// Configure does, through <see cref="AddNewTask"/> and <see cref="ManageTasks"/>.
/// </summary>
public sealed class FakeConfigureWindow(List<string>? shownLog = null) : IConfigureWindow
{
    private IShortcutAction? addNewTask;
    private IShortcutAction? manageTasks;

    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <summary>What IsOpen answers; false when not set.</summary>
    public bool IsOpen { get; set; }

    /// <summary>How many times <see cref="ConnectShortcuts"/> was called.</summary>
    public int ConnectCount { get; private set; }

    /// <summary>What <see cref="ConnectShortcuts"/> answers; true, as when both shortcuts registered, when not set.</summary>
    public bool ConnectAnswer { get; set; } = true;

    public int DisposeCount { get; private set; }
    public void Dispose() => DisposeCount++;

    /// <summary>The Add new task action Shell connected.</summary>
    /// <exception cref="InvalidOperationException">Shell hasn't connected the shortcuts.</exception>
    public IShortcutAction AddNewTask => addNewTask ?? throw NotConnected();

    /// <summary>The Manage tasks action Shell connected.</summary>
    /// <exception cref="InvalidOperationException">Shell hasn't connected the shortcuts.</exception>
    public IShortcutAction ManageTasks => manageTasks ?? throw NotConnected();

    /// <summary>The managed id of the thread of each Show call, in order.</summary>
    public List<int> ShownOnThreads { get; } = [];

    /// <inheritdoc />
    public void Show()
    {
        ShowCount++;
        ShownOnThreads.Add(Environment.CurrentManagedThreadId);
        shownLog?.Add("Configure");
    }

    /// <inheritdoc />
    public bool ConnectShortcuts(IShortcutAction addNewTask, IShortcutAction manageTasks)
    {
        ConnectCount++;
        this.addNewTask = addNewTask;
        this.manageTasks = manageTasks;
        return ConnectAnswer;
    }

    private static InvalidOperationException NotConnected() => new("No shortcut actions are connected.");
}
