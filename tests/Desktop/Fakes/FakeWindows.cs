using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.ContactList;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IAddWindow"/>: counts Show and Close calls, and adds "Add" for a Show and "Close Add"
/// for a Close to the shared log, when given one, so a test can read the order of the calls. Close hides the window.
/// </summary>
public sealed class FakeAddWindow(List<string>? shownLog = null) : IAddWindow
{
    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <summary>How many times Close was called.</summary>
    public int CloseCount { get; private set; }

    /// <summary>What IsOpen answers; false when not set. Close sets it to false.</summary>
    public bool IsOpen { get; set; }

    /// <inheritdoc />
    public void Show()
    {
        ShowCount++;
        shownLog?.Add("Add");
    }

    /// <inheritdoc />
    public void Close()
    {
        CloseCount++;
        IsOpen = false;
        shownLog?.Add("Close Add");
    }
}

/// <summary>
/// The shared fake of <see cref="IViewTasksWindow"/>: counts Show and Close calls, and logs "Task View" and
/// "Close Task View" like <see cref="FakeAddWindow"/>. Close answers <see cref="CloseAnswer"/>.
/// </summary>
public sealed class FakeViewTasksWindow(List<string>? shownLog = null) : IViewTasksWindow
{
    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <summary>How many times Close was called.</summary>
    public int CloseCount { get; private set; }

    /// <summary>What Close answers; true, as when the window closed, when not set. A true answer sets IsOpen to false.</summary>
    public bool CloseAnswer { get; set; } = true;

    /// <summary>What IsOpen answers; false when not set.</summary>
    public bool IsOpen { get; set; }

    /// <inheritdoc />
    public bool Close()
    {
        CloseCount++;
        shownLog?.Add("Close Task View");
        if (CloseAnswer)
        {
            IsOpen = false;
        }

        return CloseAnswer;
    }

    /// <summary>The managed id of the thread of each Show call, in order.</summary>
    public List<int> ShownOnThreads { get; } = [];

    /// <inheritdoc />
    public void Show()
    {
        ShowCount++;
        ShownOnThreads.Add(Environment.CurrentManagedThreadId);
        shownLog?.Add("Task View");
    }
}

/// <summary>
/// The shared fake of <see cref="IContactsWindow"/>: counts Show and Close calls, and logs "Contacts" and
/// "Close Contacts" like <see cref="FakeAddWindow"/>. Close answers <see cref="CloseAnswer"/>. A test sets
/// <see cref="IsAvailable"/> to raise <see cref="AvailableChanged"/>, as the real window does.
/// </summary>
public sealed class FakeContactsWindow(List<string>? shownLog = null) : IContactsWindow
{
    private bool isAvailable = true;

    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <summary>How many times Close was called.</summary>
    public int CloseCount { get; private set; }

    /// <summary>What Close answers; true, as when the window closed, when not set. A true answer sets IsOpen to false.</summary>
    public bool CloseAnswer { get; set; } = true;

    /// <summary>What IsOpen answers; false when not set.</summary>
    public bool IsOpen { get; set; }

    /// <summary>What IsAvailable answers; true when not set. A change raises <see cref="AvailableChanged"/>.</summary>
    public bool IsAvailable
    {
        get => isAvailable;
        set
        {
            if (isAvailable != value)
            {
                isAvailable = value;
                AvailableChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? AvailableChanged;

    /// <inheritdoc />
    public bool Close()
    {
        CloseCount++;
        shownLog?.Add("Close Contacts");
        if (CloseAnswer)
        {
            IsOpen = false;
        }

        return CloseAnswer;
    }

    /// <inheritdoc />
    public void Show()
    {
        ShowCount++;
        shownLog?.Add("Contacts");
    }
}

/// <summary>
/// The shared fake of <see cref="IConfigureWindow"/>: counts Show calls (and logs "Configure" like
/// <see cref="FakeAddWindow"/>), keeps the three shortcut actions Shell connects, and answers
/// <see cref="ConnectAnswer"/>. Unlike Configure, it registers nothing at once: a test calls the kept actions as
/// Configure does, through <see cref="AddNewTask"/>, <see cref="ManageTasks"/>, and <see cref="ConfigureAction"/>.
/// </summary>
public sealed class FakeConfigureWindow(List<string>? shownLog = null) : IConfigureWindow
{
    private IShortcutAction? addNewTask;
    private IShortcutAction? manageTasks;
    private IShortcutAction? configure;
    private IShortcutAction? contacts;

    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <summary>What IsOpen answers; false when not set.</summary>
    public bool IsOpen { get; set; }

    /// <summary>How many times Close was called. Close also makes <see cref="IsOpen"/> false.</summary>
    public int CloseCount { get; private set; }

    public void Close()
    {
        CloseCount++;
        IsOpen = false;
    }

    /// <summary>How many times <see cref="ConnectShortcuts"/> was called.</summary>
    public int ConnectCount { get; private set; }

    /// <summary>What <see cref="ConnectShortcuts"/> answers; true, as when all three shortcuts registered, when not set.</summary>
    public bool ConnectAnswer { get; set; } = true;

    public int DisposeCount { get; private set; }
    public void Dispose() => DisposeCount++;

    /// <summary>The Add new task action Shell connected.</summary>
    /// <exception cref="InvalidOperationException">Shell hasn't connected the shortcuts.</exception>
    public IShortcutAction AddNewTask => addNewTask ?? throw NotConnected();

    /// <summary>The Task View action Shell connected.</summary>
    /// <exception cref="InvalidOperationException">Shell hasn't connected the shortcuts.</exception>
    public IShortcutAction ManageTasks => manageTasks ?? throw NotConnected();

    /// <summary>The managed id of the thread of each Show call, in order.</summary>
    public List<int> ShownOnThreads { get; } = [];

    /// <summary>The Configure action Shell connected.</summary>
    /// <exception cref="InvalidOperationException">Shell hasn't connected the shortcuts.</exception>
    public IShortcutAction ConfigureAction => configure ?? throw NotConnected();

    /// <summary>The Contacts action Shell connected.</summary>
    /// <exception cref="InvalidOperationException">Shell hasn't connected the shortcuts.</exception>
    public IShortcutAction ContactsAction => contacts ?? throw NotConnected();

    /// <inheritdoc />
    public void Show()
    {
        ShowCount++;
        ShownOnThreads.Add(Environment.CurrentManagedThreadId);
        shownLog?.Add("Configure");
    }

    /// <inheritdoc />
    public bool ConnectShortcuts(IShortcutAction addNewTask, IShortcutAction manageTasks, IShortcutAction configure, IShortcutAction contacts)
    {
        ConnectCount++;
        this.addNewTask = addNewTask;
        this.manageTasks = manageTasks;
        this.configure = configure;
        this.contacts = contacts;
        return ConnectAnswer;
    }

    private static InvalidOperationException NotConnected() => new("No shortcut actions are connected.");
}
