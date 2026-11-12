using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.Configure.Tests;

internal sealed class ConfigureServices
{
    private readonly Func<ISharedTaskSettings, ISharedTaskSettings>? wrapSettings;

    public ConfigureServices(
        FakeStartWithWindows? startWithWindows = null,
        FakeSyncService? sync = null,
        Func<ISharedTaskSettings, ISharedTaskSettings>? wrapSettings = null)
    {
        this.wrapSettings = wrapSettings;
        StartWithWindows = startWithWindows ?? new FakeStartWithWindows();
        Sync = sync ?? new FakeSyncService();
        Store = TaskStore.Create(Files, new FakeImageEncoder(), new FakeTimeProvider(), @"C:\Configure tests\device");
        Store.UseRepositoryFolder(@"C:\Configure tests\clone");
    }

    public FakeStartWithWindows StartWithWindows { get; }
    public FakeSyncService Sync { get; }
    public FakeSetupActions SetupActions { get; } = new();
    public FakeFileSystem Files { get; } = new();
    public ITaskStore Store { get; }

    public IConfigureWindow Create() => ConfigureWindow.Create(
        StartWithWindows, wrapSettings?.Invoke(Store.Settings) ?? Store.Settings, Sync, SetupActions);
}
