using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>One Sync service over fakes, and the folders, addresses, and ready-made states its tests share.</summary>
internal sealed class SyncFixture
{
    public const string DefaultDeviceName = "Laptop";

    public FakeTimeProvider Time { get; } = new();

    public FakeTaskSynchronization Tasks { get; } = new();

    public FakeRepositorySelection Selection { get; }

    public FakeDeviceKeyStore Keys { get; } = new();

    public FakeFileSystem Files { get; } = new();

    public FakeGitOperations Git { get; } = new();

    public string DeviceFolder { get; } = Path.GetFullPath("sync-device");

    public string CloneFolder { get; } = Path.GetFullPath("sync-clone");

    /// <summary>The same address as <see cref="ScpAddress"/>, as Sync parses it.</summary>
    public SshAddress Address { get; } = new("git@git.example.org:me/tasks.git", "git.example.org", 22, "me/tasks.git");

    public const string ScpAddress = "git@git.example.org:me/tasks.git";

    public SyncFixture()
    {
        Selection = new FakeRepositorySelection(Tasks);
    }

    public ISyncService Create(TimeSpan? periodicInterval = null) =>
        SyncService.Create(Tasks, Files, Keys, Selection, Time, DeviceFolder, DefaultDeviceName, periodicInterval, Git);

    /// <summary>A device with a key and a selected, previously set up clone, which can sync.</summary>
    public SyncFixture Ready()
    {
        Keys.SeedKey();
        Tasks.RepositoryFolder = CloneFolder;
        Git.SeedClone(CloneFolder, Address);
        return this;
    }

    /// <summary>A key and no clone yet, which can set up a clone.</summary>
    public SyncFixture WithKey()
    {
        Keys.SeedKey();
        return this;
    }

    /// <summary>
    /// Starts the service and ends its initial pass, so a test begins from a settled, idle service: one pass started,
    /// none running.
    /// </summary>
    public async Task<ISyncService> StartedAndSettledAsync(TimeSpan? periodicInterval = null)
    {
        Ready();
        var sync = Create(periodicInterval);
        var initial = Git.ScriptPass();
        sync.Start();
        await initial.HasStarted;
        var joined = sync.SyncNowAsync(CancellationToken.None);
        initial.Complete();
        await joined;
        return sync;
    }

    public string DeviceFile(string name) => Path.Combine(DeviceFolder, name);

    public string ReadDeviceFile(string name) => System.Text.Encoding.UTF8.GetString(Files.ReadFile(DeviceFile(name)));

    public void WriteDeviceFile(string name, string content) =>
        Files.WriteFile(DeviceFile(name), System.Text.Encoding.UTF8.GetBytes(content));
}
