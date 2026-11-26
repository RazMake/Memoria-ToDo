using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Memoria.ToDo.Desktop.App;
using LibGit2Sharp;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// One test's own world: a scratch folder, an origin bare repository, and a loopback SSH server serving it, plus the
/// devices that sync with it. <see cref="RunAsync"/> is the only way to run one: after the scenario it stops the server (it
/// starts no new git and waits for every command it is serving, bounded), then every device's Sync (each bounded), then
/// deletes the scratch folder. A server or device that won't stop fails the test, but never hides the scenario's own failure.
/// </summary>
internal sealed class SyncWorld
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
    private static async Task<SyncWorld> StartAsync(Func<string, OriginRepository>? createOrigin = null, byte[]? authorizedKey = null)
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
        var keys = FakeDeviceKeyStore.Signing();
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
    /// and a cleanup failure is logged beside it; if the scenario passes, a cleanup failure fails the test. Every test uses
    /// this rather than disposing the world itself, because <c>await using</c> would let a cleanup failure replace the
    /// failure that is escaping.
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

    private static readonly TimeSpan StopBound = TimeSpan.FromSeconds(30);

    // Runs one stop and tells the bound running out (BoundExpired) from a failure the stop threw (Failure).
    private static async Task<(bool BoundExpired, Exception? Failure)> StopWithinBoundAsync(Func<ValueTask> stop)
    {
        try
        {
            return (false, await Run().WaitAsync(StopBound));
        }
        catch (TimeoutException)
        {
            return (true, null);
        }

        // Run never faults, so only the bound can make the wait throw, even when the stop throws its own TimeoutException
        // or throws before returning its task.
        async Task<Exception?> Run()
        {
            try
            {
                await stop();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    // The server goes first: closing its connections is what unblocks a client still reading from a stalled remote.
    // Every device is stopped, each bounded, even when an earlier one failed, so a hung or broken client cannot hang the
    // run or hide another. Every server and device failure is reported together, the server's first, and leaves the
    // scratch folder in place. Otherwise a scratch folder that can't be deleted is reported with the server's command
    // timeline, since a git the server started is the likeliest holder.
    private async Task<Exception?> CleanUpAsync()
    {
        var failures = new List<Exception>();
        var (serverTimedOut, serverError) = await StopWithinBoundAsync(Server.DisposeAsync);
        if (serverTimedOut)
        {
            failures.Add(new TimeoutException(
                $"The test SSH server did not finish its commands within {StopBound.TotalSeconds} seconds.{ServerCommands()}"));
        }
        else if (serverError is not null)
        {
            failures.Add(new InvalidOperationException(
                $"The test SSH server failed while stopping: {serverError.Message}{ServerCommands()}",
                serverError));
        }

        foreach (var device in devices)
        {
            var (timedOut, error) = await StopWithinBoundAsync(device.DisposeAsync);
            if (timedOut)
            {
                failures.Add(new TimeoutException(
                    $"Sync did not finish stopping within {StopBound.TotalSeconds} seconds for the device with clone folder {device.CloneFolder}."));
            }
            else if (error is not null)
            {
                failures.Add(new InvalidOperationException(
                    $"Sync failed while stopping for the device with clone folder {device.CloneFolder}: {error.Message}",
                    error));
            }
        }

        if (failures.Count > 0)
        {
            return new AggregateException($"The world did not stop cleanly. Scratch folder left in place: {Root}", failures);
        }

        try
        {
            DeleteScratch(Root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new IOException($"The scratch folder {Root} could not be deleted: {ex.Message}{ServerCommands()}", ex);
        }

        return null;
    }

    private string ServerCommands() =>
        $"{Environment.NewLine}The test SSH server's commands:{Environment.NewLine}{Server.Describe()}";

    internal static void DeleteScratch(string folder)
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
        Keys = FakeDeviceKeyStore.Signing();
        Repository = new SyncedRepository(Serilog.Core.Logger.None);
        Store = TestTaskStore.Create(Files, new FakeImageEncoder(), Time, DeviceFolder, Repository);
        Contacts = ContactStore.Create(Files, Repository, Serilog.Core.Logger.None);
        Sync = SyncService.Create(Repository, Store.Synchronization, Contacts.Files, Files, syncKeys ?? Keys, new StoreSelection(Repository), syncClock ?? Time, DeviceFolder, name, periodicInterval: null, logger: Serilog.Core.Logger.None);
    }

    /// <summary>The synced repository the device's Tasks store, Contacts store, and Sync share.</summary>
    public SyncedRepository Repository { get; }

    public WindowsFileSystem Files { get; } = new();

    public string DeviceFolder { get; }

    public string CloneFolder { get; }

    public FakeTimeProvider Time { get; }

    public FakeDeviceKeyStore Keys { get; }

    public ITaskStore Store { get; }

    public IContactStore Contacts { get; }

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

    private sealed class StoreSelection(SyncedRepository repository) : IRepositorySelection
    {
        public void Load() { }

        public void Select(string folder) => repository.Select(folder);
    }
}
