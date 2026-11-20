using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>The four global shortcuts: the ones that open Add, Task View, Configure, and Contacts.</summary>
public enum ShortcutKind
{
    AddNewTask,
    ManageTasks,
    Configure,
    Contacts,
}

/// <summary>A tray app built on the shared fakes, the setup every Shell test shares.</summary>
internal sealed class ShellHarness
{
    /// <summary>The action Shell gave Configure for the shortcut. Shell must have started.</summary>
    public IShortcutAction ActionOf(ShortcutKind kind) => kind switch
    {
        ShortcutKind.AddNewTask => Configure.AddNewTask,
        ShortcutKind.ManageTasks => Configure.ManageTasks,
        ShortcutKind.Configure => Configure.ConfigureAction,
        _ => Configure.ContactsAction,
    };

    /// <summary>How many times the window the shortcut opens was shown.</summary>
    public int ShownBy(ShortcutKind kind) => kind switch
    {
        ShortcutKind.AddNewTask => Add.ShowCount,
        ShortcutKind.ManageTasks => ViewTasks.ShowCount,
        ShortcutKind.Configure => Configure.ShowCount,
        _ => Contacts.ShowCount,
    };

    /// <summary>How many times the windows of the other task, Task View, and Contacts shortcuts were shown (Configure's never counts).</summary>
    public int ShownByOtherThan(ShortcutKind kind) =>
        Add.ShowCount + ViewTasks.ShowCount + Contacts.ShowCount - ShownBy(kind);

    /// <summary>What the window the shortcut opens writes in <see cref="Shown"/> when it's shown.</summary>
    public static string WindowOf(ShortcutKind kind) => kind switch
    {
        ShortcutKind.AddNewTask => "Add",
        ShortcutKind.ManageTasks => "Task View",
        ShortcutKind.Configure => "Configure",
        _ => "Contacts",
    };

    /// <summary>Makes the window the shortcut opens open or closed, as its port answers.</summary>
    public void SetOpen(ShortcutKind kind, bool open)
    {
        switch (kind)
        {
            case ShortcutKind.AddNewTask:
                Add.IsOpen = open;
                break;
            case ShortcutKind.ManageTasks:
                ViewTasks.IsOpen = open;
                break;
            case ShortcutKind.Configure:
                Configure.IsOpen = open;
                break;
            default:
                Contacts.IsOpen = open;
                break;
        }
    }

    public FakeTrayIcon TrayIcon { get; } = new();
    public FakeAddWindow Add { get; }
    public FakeViewTasksWindow ViewTasks { get; }
    public FakeContactsWindow Contacts { get; }
    public FakeConfigureWindow Configure { get; }
    public FakeLogsWindow Logs { get; }

    /// <summary>The entries Shell logged, through the logger it was given.</summary>
    public InMemoryLogSink Log { get; } = new();

    public FakeUpdateLifetime Updates { get; } = new();
    public FakeGlobalShortcut GlobalShortcut { get; } = new();
    public FakeOneInstance OneInstance { get; } = new();
    public FakeSyncService Sync { get; } = new();
    public int EndAppCalls { get; private set; }
    public IAppLifetime Lifetime { get; }

    /// <summary>The window calls, in order: "Add", "Task View", "Contacts", and "Configure" for a Show, "Close Add", "Close Task View", and "Close Contacts" for a Close, and "Logs" for a Show of the Logs window.</summary>
    public List<string> Shown { get; } = [];

    /// <summary>The UI thread Shell was given: a fake the test drives, unless the test passed a real one.</summary>
    public FakeUiThread UiThread { get; } = new();

    /// <summary>
    /// Builds the tray app. A start at sign-in is the default, since it shows no window; pass false for a start by hand.
    /// A test that needs the real UI thread passes <paramref name="realUiThread"/>.
    /// </summary>
    public ShellHarness(bool startedAtSignIn = true, Func<IUiThread>? realUiThread = null)
    {
        Add = new FakeAddWindow(Shown);
        ViewTasks = new FakeViewTasksWindow(Shown);
        Contacts = new FakeContactsWindow(Shown);
        Configure = new FakeConfigureWindow(Shown);
        Logs = new FakeLogsWindow(Shown);
        Lifetime = TrayApp.Create(
            TrayIcon, Add, ViewTasks, Contacts, Configure, Logs, Updates, GlobalShortcut, OneInstance, startedAtSignIn, Sync,
            Log.CreateLogger(), realUiThread ?? (() => UiThread));
    }

    /// <summary>Starts the app, with an end action that counts its calls.</summary>
    public void Start() => Lifetime.Start(() => EndAppCalls++);
}
