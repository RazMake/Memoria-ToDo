using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>The two global shortcuts: the one that opens Add and the one that opens View Tasks.</summary>
public enum ShortcutKind
{
    AddNewTask,
    ManageTasks,
}

/// <summary>A tray app built on the shared fakes, the setup every Shell test shares.</summary>
internal sealed class ShellHarness
{
    /// <summary>The action Shell gave Configure for the shortcut. Shell must have started.</summary>
    public IShortcutAction ActionOf(ShortcutKind kind) =>
        kind == ShortcutKind.AddNewTask ? Configure.AddNewTask : Configure.ManageTasks;

    /// <summary>How many times the window the shortcut opens was shown.</summary>
    public int ShownBy(ShortcutKind kind) => kind == ShortcutKind.AddNewTask ? Add.ShowCount : ViewTasks.ShowCount;

    /// <summary>How many times the window the other shortcut opens was shown.</summary>
    public int ShownByOtherThan(ShortcutKind kind) =>
        ShownBy(kind == ShortcutKind.AddNewTask ? ShortcutKind.ManageTasks : ShortcutKind.AddNewTask);

    public FakeTrayIcon TrayIcon { get; } = new();
    public FakeAddWindow Add { get; }
    public FakeViewTasksWindow ViewTasks { get; }
    public FakeConfigureWindow Configure { get; }
    public FakeAppUpdates Updates { get; } = new();
    public FakeGlobalShortcut GlobalShortcut { get; } = new();
    public FakeOneInstance OneInstance { get; } = new();
    public FakeTimeProvider Time { get; } = new();
    public FakeSyncService Sync { get; } = new();
    public int EndAppCalls { get; private set; }
    public IAppLifetime Lifetime { get; }

    /// <summary>The windows shown, in order: "Add", "View Tasks", and "Configure".</summary>
    public List<string> Shown { get; } = [];

    /// <summary>
    /// Builds the tray app. A start at sign-in is the default, since it shows no window; pass false for a start by hand.
    /// </summary>
    public ShellHarness(bool startedAtSignIn = true)
    {
        Add = new FakeAddWindow(Shown);
        ViewTasks = new FakeViewTasksWindow(Shown);
        Configure = new FakeConfigureWindow(Shown);
        Lifetime = TrayApp.Create(
            TrayIcon, Add, ViewTasks, Configure, Updates, GlobalShortcut, OneInstance, startedAtSignIn, Time, Sync);
    }

    /// <summary>Starts the app, with an end action that counts its calls.</summary>
    public void Start() => Lifetime.Start(() => EndAppCalls++);
}
