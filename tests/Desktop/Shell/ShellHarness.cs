using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>A tray app built on the shared fakes, the setup every Shell test shares.</summary>
internal sealed class ShellHarness
{
    public FakeTrayIcon TrayIcon { get; } = new();
    public FakeAddWindow Add { get; } = new();
    public FakeViewTasksWindow ViewTasks { get; } = new();
    public FakeConfigureWindow Configure { get; } = new();
    public FakeAppUpdates Updates { get; } = new();
    public FakeTimeProvider Time { get; } = new();
    public int EndAppCalls { get; private set; }
    public IAppLifetime Lifetime { get; }

    public ShellHarness() =>
        Lifetime = TrayApp.Create(TrayIcon, Add, ViewTasks, Configure, Updates, Time);

    /// <summary>Starts the app, with an end action that counts its calls.</summary>
    public void Start() => Lifetime.Start(() => EndAppCalls++);
}
