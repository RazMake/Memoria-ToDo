using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.Configure.Tests;

internal sealed class ConfigureServices
{
    /// <param name="settings">The shared settings the window gets; the real store's when null.</param>
    public ConfigureServices(
        FakeStartWithWindows? startWithWindows = null, FakeSyncService? sync = null, ISharedTaskSettings? settings = null)
    {
        StartWithWindows = startWithWindows ?? new FakeStartWithWindows();
        Sync = sync ?? new FakeSyncService();
        Store = TestTaskStore.Create(new FakeFileSystem(), new FakeImageEncoder(), new FakeTimeProvider(), @"C:\Configure tests\device");
        Store.UseRepositoryFolder(@"C:\Configure tests\clone");
        Settings = settings ?? Store.Settings;
    }

    public FakeStartWithWindows StartWithWindows { get; }
    public FakeSyncService Sync { get; }
    public FakeSetupActions SetupActions { get; } = new();
    public CloseWindowKeys CloseWindowKeys { get; } = new();
    public SaveKeys SaveKeys { get; } = new();
    public FakeTheme Theme { get; } = new();
    public FakeUpdateStatus Updates { get; } = new();
    public ITaskStore Store { get; }
    public ISharedTaskSettings Settings { get; }

    /// <summary>The entries the window logged, through the logger it was given.</summary>
    public InMemoryLogSink Log { get; } = new();

    public IConfigureWindow Create() =>
        ConfigureWindow.Create(StartWithWindows, Settings, Sync, SetupActions, CloseWindowKeys, Theme, Updates, SaveKeys, Log.CreateLogger());
}
