using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.Configure.Tests;

internal sealed class ConfigureServices
{
    public ConfigureServices(FakeStartWithWindows? startWithWindows = null, FakeSyncService? sync = null)
    {
        StartWithWindows = startWithWindows ?? new FakeStartWithWindows();
        Sync = sync ?? new FakeSyncService();
        Store = TaskStore.Create(new FakeFileSystem(), new FakeImageEncoder(), new FakeTimeProvider(), @"C:\Configure tests\device");
        Store.UseRepositoryFolder(@"C:\Configure tests\clone");
    }

    public FakeStartWithWindows StartWithWindows { get; }
    public FakeSyncService Sync { get; }
    public FakeSetupActions SetupActions { get; } = new();
    public CloseWindowKeys CloseWindowKeys { get; } = new();
    public FakeTheme Theme { get; } = new();
    public ITaskStore Store { get; }

    public IConfigureWindow Create() =>
        ConfigureWindow.Create(StartWithWindows, Store.Settings, Sync, SetupActions, CloseWindowKeys, Theme);
}
