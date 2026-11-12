using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Memoria.ToDo.Desktop.App;
using LibGit2Sharp;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// One test's own world: a scratch folder, an origin bare repository, and a loopback SSH server serving it, plus the
/// devices that sync with it. Disposing stops the server, then every device's Sync (each bounded), then deletes the scratch
/// folder; a device that won't stop fails the test. <see cref="RunAsync"/> keeps a failing scenario's own failure first.
/// </summary>
internal sealed class SyncWorld : IAsyncDisposable
{
    private readonly List<Device> devices = [];

    private SyncWorld(string root, OriginRepository origin, TestSshServer server)
    {
        Root = root;
        Origin = origin;
        Server = server;
    }

    public string Root { get; }

    public OriginRepository Origin { get; }

    public TestSshServer Server { get; }

    public string Address => $"ssh://git@127.0.0.1:{Server.Port}/origin.git";

    /// <param name="createOrigin">Makes the origin at the given path; an unborn one by default.</param>
    /// <param name="authorizedKey">The one client key the server accepts; the shared fake device key by default.</param>
    public static async Task<SyncWorld> StartAsync(Func<string, OriginRepository>? createOrigin = null, byte[]? authorizedKey = null)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "scratch", Guid.NewGuid().ToString("N"));
        var served = Directory.CreateDirectory(Path.Combine(root, "served")).FullName;
        var origin = (createOrigin ?? OriginRepository.CreateUnborn)(Path.Combine(served, "origin.git"));
        var server = new TestSshServer(served, authorizedKey ?? DeviceKey());
        await server.StartAsync();
        return new SyncWorld(root, origin, server);
    }

    /// <summary>The public-key blob every <see cref="Device"/> signs with.</summary>
    public static byte[] DeviceKey()
    {
        var keys = new FakeDeviceKeyStore();
        keys.SeedKey();
        return keys.PublicKeyBlob;
    }

    /// <summary>A public-key blob of a key no device has.</summary>
    public static byte[] StrangerKey()
    {
        using var rsa = RSA.Create(2048);
        return FakeDeviceKeyStore.Blob(rsa);
    }

    /// <param name="syncClock">The clock Sync runs on; the device's fake clock by default. A test that waits for Sync's real network timeouts passes <see cref="TimeProvider.System"/>, since a frozen fake clock would never let them expire.</param>
    public Device AddDevice(string name, TimeProvider? syncClock = null, IDeviceKeyStore? syncKeys = null)
    {
        var device = new Device(this, name, syncClock, syncKeys);
        devices.Add(device);
        return device;
    }

    /// <summary>
    /// Runs one scenario in a fresh world and always cleans it up. If the scenario fails, its failure is the one reported
    /// and a cleanup failure is logged beside it; if the scenario passes, a cleanup failure fails the test. Scenarios that
    /// can leave a stalled client behind use this, because <c>await using</c> cannot see the failure that is escaping.
    /// </summary>
    public static async Task RunAsync(Func<SyncWorld, Task> scenario, Func<string, OriginRepository>? createOrigin = null, byte[]? authorizedKey = null)
    {
        var world = await StartAsync(createOrigin, authorizedKey);
        ExceptionDispatchInfo? scenarioFailure = null;
        try
        {
            await scenario(world);
        }
        catch (Exception ex)
        {
            scenarioFailure = ExceptionDispatchInfo.Capture(ex);
        }

        var cleanupFailure = await world.CleanUpAsync();
        if (scenarioFailure is not null)
        {
            if (cleanupFailure is not null)
            {
                TestContext.Current.SendDiagnosticMessage($"The scenario failed, and its cleanup also failed: {cleanupFailure.Message}");
            }

            scenarioFailure.Throw();
        }

        if (cleanupFailure is not null)
        {
            throw cleanupFailure;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (await CleanUpAsync() is { } failure)
        {
            throw failure;
        }
    }

    // The server goes first: closing its connections is what unblocks a client still reading from a stalled remote.
    // Each device's cleanup is bounded so a hung client cannot hang the run; a device that does not finish leaves its
    // scratch folder in place and is reported as the failure.
    private async Task<TimeoutException?> CleanUpAsync()
    {
        Server.Dispose();
        var stuck = new List<string>();
        foreach (var device in devices)
        {
            try
            {
                await device.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                stuck.Add(device.CloneFolder);
            }
        }

        if (stuck.Count > 0)
        {
            return new TimeoutException($"Sync did not finish stopping within 30 seconds for: {string.Join(", ", stuck)}. Scratch folder left in place: {Root}");
        }

        DeleteScratch(Root);
        return null;
    }

    private static void DeleteScratch(string folder)
    {
        if (!Directory.Exists(folder))
            return;
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(folder, recursive: true);
    }
}

/// <summary>One device: its own Tasks store, device folder, clone folder, key store, and real Sync service.</summary>
internal sealed class Device : IAsyncDisposable
{
    private readonly SyncWorld world;

    public Device(SyncWorld world, string name, TimeProvider? syncClock = null, IDeviceKeyStore? syncKeys = null)
    {
        this.world = world;
        var folder = Path.Combine(world.Root, name);
        DeviceFolder = Path.Combine(folder, "device");
        CloneFolder = Path.Combine(folder, "clone");
        Time = new FakeTimeProvider();
        Keys = new FakeDeviceKeyStore();
        Store = TaskStore.Create(Files, new FakeImageEncoder(), Time, DeviceFolder);
        Sync = SyncService.Create(Store.Synchronization, Files, syncKeys ?? Keys, new StoreSelection(Store), syncClock ?? Time, DeviceFolder, name, periodicInterval: null);
    }

    public WindowsFileSystem Files { get; } = new();

    public string DeviceFolder { get; }

    public string CloneFolder { get; }

    public FakeTimeProvider Time { get; }

    public FakeDeviceKeyStore Keys { get; }

    public ITaskStore Store { get; }

    public ISyncService Sync { get; }

    /// <summary>Makes a key, clones the origin, and trusts the server's host key when it is offered, as the person setting up would.</summary>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await Sync.Setup.GenerateKeyAsync(cancellationToken);
        if (!await TryCloneAsync(cancellationToken))
        {
            await Sync.Setup.AcceptHostKeyAsync(Sync.Setup.State.HostKey!.Fingerprint, cancellationToken);
            Assert.True(await TryCloneAsync(cancellationToken));
        }
    }

    /// <summary>Connects, then runs the first sync that makes the device configured, as Configure does. Fails the test if that sync fails.</summary>
    public async Task ConnectAndSyncAsync(CancellationToken cancellationToken)
    {
        await ConnectAsync(cancellationToken);
        var status = await Sync.SyncNowAsync(cancellationToken);
        Assert.True(status.Failure is null && Sync.Setup.State.IsConfigured,
            $"The first sync did not configure the device: state {status.State}, failure {status.Failure}.");
    }

    public async Task<bool> TryCloneAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Sync.Setup.CloneAsync(world.Address, CloneFolder, cancellationToken);
            return true;
        }
        catch (Exception) when (Sync.Setup.State.HostKey is { CanAccept: true })
        {
            return false;
        }
    }

    public TaskItem AddTask(string title, string description)
    {
        var draft = Store.StartDraft();
        return Store.Add(draft with { Title = title, Description = description });
    }

    /// <summary>Saves an edit of one task's title and description, keeping its tags.</summary>
    public TaskItem EditTask(string id, string? title = null, string? description = null)
    {
        var started = Store.All.Single(t => t.Id == id);
        return Store.SaveEdit(started, new TaskDraft(id, title ?? started.Title, description ?? started.Description, started.Tags));
    }

    public TaskItem Item(string id) => Store.All.Single(t => t.Id == id);

    /// <summary>The clone's working files outside .git with their hashes, and its HEAD, to prove a refusal changed nothing.</summary>
    public string Snapshot()
    {
        var files = Directory.EnumerateFiles(CloneFolder, "*", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(Path.Combine(CloneFolder, ".git") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(f => $"{Path.GetRelativePath(CloneFolder, f)}={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))}");
        using var repo = new Repository(CloneFolder);
        return string.Join('\n', files.Append($"HEAD={repo.Head.CanonicalName}@{repo.Head.Tip?.Sha}"));
    }

    public ValueTask DisposeAsync() => Sync.DisposeAsync();

    private sealed class StoreSelection(ITaskStore store) : IRepositorySelection
    {
        public void Load() { }

        public void Select(string folder) => store.UseRepositoryFolder(folder);
    }
}
