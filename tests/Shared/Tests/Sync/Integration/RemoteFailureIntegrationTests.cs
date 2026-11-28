using LibGit2Sharp;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// How a failing remote shows in status, against the failure table in docs/architecture/sync.md: only an unreachable or
/// stalled host is a connection failure; a missing repository or a key without write access is an error.
/// </summary>
[Integration]
public sealed class RemoteFailureIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    // The documented stalled-network timeout is 30 seconds; the rest is room for the 5-second graceful end and cleanup.
    private static readonly TimeSpan StallBound = TimeSpan.FromSeconds(50);

    [Fact]
    public Task A_remote_that_stops_answering_mid_pass_ends_the_pass_as_a_connection_failure_and_a_later_pass_succeeds() =>
        SyncWorld.RunAsync(StalledPassScenario, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));

    private static async Task StalledPassScenario(SyncWorld world)
    {
        var ct = TestContext.Current.CancellationToken;
        var keys = new WeakTrackingKeyStore(Memoria.ToDo.Shared.Fakes.FakeDeviceKeyStore.Signing());
        var a = world.AddDevice("a", TimeProvider.System, keys);
        await a.ConnectAsync(ct);
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Server.BeforeCommand = async (_, _) =>
        {
            arrived.TrySetResult();
            await never.Task;
        };
        a.AddTask("Pending", "Waits for the network");

        try
        {
            var pass = a.Sync.SyncNowAsync(ct);
            await arrived.Task.WaitAsync(Bound, ct);

            // No caller cancels: only the stalled-network timeout can end this pass.
            var status = await pass.WaitAsync(StallBound, ct);

            Assert.Equal(SyncFailure.Connection, status.Failure);
            Assert.True(status.HasPendingChanges);
        }
        finally
        {
            world.Server.BeforeCommand = null;
            never.TrySetResult();
        }

        var retried = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Idle, retried.State);
        Assert.False(retried.HasPendingChanges);

        await a.Sync.StopAsync(ct).WaitAsync(Bound, ct);

        // Everything Sync used for the passes, including what libgit2's transport kept, must be unreachable now.
        Assert.Equal(0, keys.OpenedKeysStillReachable());
    }

    [Fact]
    public async Task A_command_that_finishes_its_protocol_but_exits_nonzero_is_never_a_clean_sync()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            var a = world.AddDevice("a");
            await a.ConnectAsync(ct);
            world.Server.ExitCodeAfterServing = verb => verb == "receive-pack" ? 128u : null;
            a.AddTask("Pending", "The push is refused after its data was read");

            var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

            Assert.Equal(SyncState.Error, status.State);
            Assert.Equal(SyncFailure.Repository, status.Failure);
            Assert.True(status.HasPendingChanges);
        }, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
    }

    [Fact]
    public async Task A_repository_the_host_does_not_have_is_an_error_naming_the_repository_not_an_offline_state()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            var a = world.AddDevice("a");
            await a.ConnectAsync(ct);
            using (var clone = new Repository(a.CloneFolder))
                clone.Network.Remotes.Update("origin", r => r.Url = $"ssh://git@127.0.0.1:{world.Server.Port}/missing.git");
            a.AddTask("Pending", "Nowhere to go");

            var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

            Assert.Equal(SyncState.Error, status.State);
            Assert.Equal(SyncFailure.Repository, status.Failure);
        }, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
    }

    /// <summary>What a host says when it refuses the push, and the failure Sync must name: an explicit denial of the key or of write permission is Authentication; any other reason, even one that mentions read-only, is Repository.</summary>
    public static TheoryData<string, SyncFailure> Refusals => new()
    {
        { "fatal: Permission denied", SyncFailure.Authentication },
        { "fatal: read-only file system", SyncFailure.Repository },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_key_that_lost_write_access_fails_the_push_as_an_error_named_by_the_hosts_explanation_not_an_offline_state(string hostText, SyncFailure expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            var a = world.AddDevice("a");
            await a.ConnectAsync(ct);
            world.Server.AllowPush = false;
            world.Server.DenialText = hostText;
            var task = a.AddTask("Pending", "Cannot be pushed");

            var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

            Assert.Equal(SyncState.Error, status.State);
            Assert.Equal(expected, status.Failure);
            Assert.Contains(hostText, status.Details);
            Assert.True(status.HasPendingChanges);
            Assert.Null(world.Origin.ReadOnMain($"tasks/{task.Id}.json"));
        }, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task The_first_sync_of_an_existing_clone_fails_as_the_hosts_explanation_says_when_the_key_may_not_push_and_the_device_stays_not_configured(string hostText, SyncFailure expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            world.Server.AllowPush = false;
            world.Server.DenialText = hostText;
            var device = world.AddDevice("a");
            Repository.Clone(world.Origin.Path, device.CloneFolder);

            var status = await FirstSyncOfExistingCloneAsync(world, device, ct);

            Assert.False(device.Sync.Setup.State.IsConfigured);
            Assert.Equal(SyncState.Error, status.State);
            Assert.Equal(expected, status.Failure);
            Assert.Contains(hostText, status.Details);
        }, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
    }

    [Fact]
    public async Task The_first_sync_of_an_existing_clone_names_a_connection_failure_when_the_host_refuses_to_start_the_write_check()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            world.Server.RefuseCommand = verb => verb == "receive-pack";
            var device = world.AddDevice("a", TimeProvider.System);
            Repository.Clone(world.Origin.Path, device.CloneFolder);

            // The refused request leaves the channel open and SSH.NET ignores the refusal, so Sync sees a stall: only the real clock's network watchdog ends it.
            var status = await FirstSyncOfExistingCloneAsync(world, device, ct, StallBound);

            // Fetching worked, so the host is reachable and the key is known; a refused session is no verdict on write access.
            Assert.False(device.Sync.Setup.State.IsConfigured);
            Assert.Equal(SyncFailure.Connection, status.Failure);
        }, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
    }

    [Fact]
    public async Task An_existing_up_to_date_clone_with_a_read_only_key_is_not_configured_until_the_key_may_push()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            // Another device synced first, so the origin holds the settings.json a sync creates and the first sync below has
            // nothing to commit: it reaches the write check, not a push.
            await world.AddDevice("other").ConnectAndSyncAsync(ct);
            world.Server.AllowPush = false;
            world.Server.DenialText = "ERROR: The key is marked as read only";
            var device = world.AddDevice("a");
            Repository.Clone(world.Origin.Path, device.CloneFolder);
            using (var repo = new Repository(device.CloneFolder))
            {
                // The arrangement itself is checked: the clone is at the origin's tip with settings.json and nothing uncommitted.
                Assert.Equal(world.Origin.MainTip, repo.Head.Tip.Sha);
                Assert.NotNull(repo.Head.Tip["settings.json"]);
                Assert.False(repo.RetrieveStatus(new StatusOptions()).IsDirty);
            }

            await AssertNotConfiguredUntilTheKeyMayPushAsync(world, device, ct);
        }, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
    }

    [Fact]
    public async Task An_existing_empty_clone_with_nothing_to_commit_and_a_read_only_key_is_not_configured_until_the_key_may_push()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            world.Server.AllowPush = false;
            world.Server.DenialText = "ERROR: The key is marked as read only";
            var device = world.AddDevice("a");

            // A valid repository with no commit whose local excludes hide the only managed file Sync would create, so a pass has nothing to commit.
            Repository.Init(device.CloneFolder);
            var info = Directory.CreateDirectory(Path.Combine(device.CloneFolder, ".git", "info")).FullName;
            File.WriteAllText(Path.Combine(info, "exclude"), "settings.json\n");
            using (var repo = new Repository(device.CloneFolder))
            {
                // The arrangement itself is checked: no commit yet, and the exclude really hides the managed file.
                Assert.Null(repo.Head.Tip);
                File.WriteAllText(Path.Combine(device.CloneFolder, "settings.json"), "{}");
                Assert.True(repo.Ignore.IsPathIgnored("settings.json"));
                File.Delete(Path.Combine(device.CloneFolder, "settings.json"));
            }

            await AssertNotConfiguredUntilTheKeyMayPushAsync(world, device, ct);
        });
    }

    [Fact]
    public async Task A_clone_the_host_denies_write_access_removes_every_file_it_cloned_and_fails_with_that_denial()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            var a = await TrustingDeviceWithAReadOnlyKeyAsync(world, ct);
            var originTip = world.Origin.MainTip;

            // The arrangement itself is checked: the write check is the clone's last remote step, so the cloned files exist by then.
            var clonedBeforeTheCheck = false;
            world.Server.BeforeCommand = (verb, _) =>
            {
                if (verb == "receive-pack")
                {
                    clonedBeforeTheCheck = File.Exists(Path.Combine(a.CloneFolder, "notes.txt"));
                }

                return Task.CompletedTask;
            };

            await Assert.ThrowsAnyAsync<IOException>(() => a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, ct).WaitAsync(Bound, ct));
            var status = a.Sync.Status;

            Assert.True(clonedBeforeTheCheck);
            Assert.False(Directory.Exists(a.CloneFolder) && Directory.EnumerateFileSystemEntries(a.CloneFolder).Any(),
                "Cloned files remain in the clone folder after the refused clone.");
            Assert.Equal(SyncState.Error, status.State);
            Assert.Equal(SyncFailure.Authentication, status.Failure);
            Assert.Contains("marked as read only", status.Details);
            Assert.False(a.Sync.Setup.State.IsConfigured);
            Assert.Null(a.Sync.Setup.State.RepositoryFolder);
            Assert.Equal(originTip, world.Origin.MainTip);
        }, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
    }

    [Fact]
    public async Task A_clone_the_host_denies_write_access_keeps_that_denial_as_its_failure_when_undoing_the_clone_also_fails()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test needs Windows: an open file only blocks its own deletion there.");
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            var a = await TrustingDeviceWithAReadOnlyKeyAsync(world, ct);
            var originTip = world.Origin.MainTip;

            // The write check is the clone's last remote step, so by the time git asks for receive-pack the clone's files exist.
            // A file held open without sharing then makes undoing the clone fail, as an editor or scanner holding a file would.
            FileStream? held = null;
            world.Server.BeforeCommand = (verb, _) =>
            {
                if (verb == "receive-pack")
                {
                    held = new FileStream(Path.Combine(a.CloneFolder, "held.tmp"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                }

                return Task.CompletedTask;
            };
            try
            {
                await Assert.ThrowsAnyAsync<IOException>(() => a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, ct).WaitAsync(Bound, ct));
                var status = a.Sync.Status;

                Assert.NotNull(held);
                Assert.Equal(SyncState.Error, status.State);
                Assert.Equal(SyncFailure.Authentication, status.Failure);
                Assert.Contains("marked as read only", status.Details);
                Assert.Contains("held.tmp", status.Details);
                Assert.False(a.Sync.Setup.State.IsConfigured);
                Assert.Equal(originTip, world.Origin.MainTip);
            }
            finally
            {
                held?.Dispose();
            }
        }, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
    }

    // A new device with a key and the host's key trusted, not yet cloned, whose key the host lets fetch but refuses to let push.
    private static async Task<Device> TrustingDeviceWithAReadOnlyKeyAsync(SyncWorld world, CancellationToken ct)
    {
        world.Server.AllowPush = false;
        world.Server.DenialText = "ERROR: The key is marked as read only";
        var device = world.AddDevice("a");
        await device.Sync.Setup.GenerateKeyAsync(ct);
        Assert.False(await device.TryCloneAsync(ct));
        await device.Sync.Setup.AcceptHostKeyAsync(device.Sync.Setup.State.HostKey!.Fingerprint, ct);
        return device;
    }

    // Setup with a key the host refuses to let push must not finish, however the sync then ends; once the key may push, it finishes and the origin is as it was.
    private static async Task AssertNotConfiguredUntilTheKeyMayPushAsync(SyncWorld world, Device device, CancellationToken ct)
    {
        var originTip = world.Origin.MainTip;

        var status = await FirstSyncOfExistingCloneAsync(world, device, ct);

        Assert.False(device.Sync.Setup.State.IsConfigured);
        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.Authentication, status.Failure);
        Assert.False(status.HasPendingChanges);
        Assert.Equal(originTip, world.Origin.MainTip);

        world.Server.AllowPush = true;
        await device.Sync.Setup.UseExistingAsync(world.Address, device.CloneFolder, ct);
        var allowed = await device.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Idle, allowed.State);
        Assert.True(device.Sync.Setup.State.IsConfigured);
    }

    // Using an existing clone contacts no host and checks no write access, so it succeeds; the first sync verifies push access.
    // The person setting up trusts the host key when that sync first offers it, and the sync runs again.
    // The bound defaults to Bound; a test that waits for the network watchdog on the real clock passes StallBound.
    private static async Task<ISyncStatus> FirstSyncOfExistingCloneAsync(SyncWorld world, Device device, CancellationToken ct, TimeSpan? bound = null)
    {
        var limit = bound ?? Bound;
        await device.Sync.Setup.GenerateKeyAsync(ct);
        await device.Sync.Setup.UseExistingAsync(world.Address, device.CloneFolder, ct);
        var status = await device.Sync.SyncNowAsync(ct).WaitAsync(limit, ct);
        if (status.Failure == SyncFailure.UnknownHostKey)
        {
            await device.Sync.Setup.AcceptHostKeyAsync(device.Sync.Setup.State.HostKey!.Fingerprint, ct);
            status = await device.Sync.SyncNowAsync(ct).WaitAsync(limit, ct);
        }

        return status;
    }
}
