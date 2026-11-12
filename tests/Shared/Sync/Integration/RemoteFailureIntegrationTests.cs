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
        var a = world.AddDevice("a", TimeProvider.System);
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
    }

    [Fact]
    public async Task A_stalled_pass_that_timed_out_leaves_nothing_holding_the_device_key_once_sync_has_stopped() =>
        await SyncWorld.RunAsync(StalledPassKeyScenario, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));

    private static async Task StalledPassKeyScenario(SyncWorld world)
    {
        var ct = TestContext.Current.CancellationToken;
        var keys = new WeakTrackingKeyStore(new Memoria.ToDo.Shared.Fakes.FakeDeviceKeyStore());
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
        }
        finally
        {
            world.Server.BeforeCommand = null;
            never.TrySetResult();
        }

        await a.Sync.StopAsync(ct).WaitAsync(Bound, ct);

        // Everything Sync used for the pass, including what libgit2's transport kept, must be unreachable now.
        Assert.Equal(0, keys.OpenedKeysStillReachable());
    }

    [Fact]
    public async Task A_command_that_finishes_its_protocol_but_exits_nonzero_is_never_a_clean_sync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        world.Server.ExitCodeAfterServing = verb => verb == "receive-pack" ? 128u : null;
        a.AddTask("Pending", "The push is refused after its data was read");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.Repository, status.Failure);
    }

    [Theory]
    [InlineData(ExitReport.NoStatus)]
    [InlineData(ExitReport.Signal)]
    public async Task A_command_that_ends_without_an_exit_status_is_never_a_clean_sync(ExitReport report)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        world.Server.Report = report;
        a.AddTask("Pending", "Success of the push is unproven");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        // The command completed but never reported success: that is a repository problem, not a lost connection.
        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.Repository, status.Failure);
        Assert.False(string.IsNullOrWhiteSpace(status.Details));
    }

    [Fact]
    public Task A_command_whose_output_ended_but_never_exits_is_cut_off_after_the_five_second_graceful_wait_as_a_connection_failure() =>
        SyncWorld.RunAsync(NeverExitsScenario, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));

    private static async Task NeverExitsScenario(SyncWorld world)
    {
        var ct = TestContext.Current.CancellationToken;
        var a = world.AddDevice("a", TimeProvider.System);
        await a.ConnectAsync(ct);
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Server.BeforeExit = _ =>
        {
            finished.TrySetResult();
            return held.Task;
        };
        a.AddTask("Pending", "The command never reports its exit");

        try
        {
            var pass = a.Sync.SyncNowAsync(ct);
            await finished.Task.WaitAsync(Bound, ct);

            // The graceful wait after EOF is 5 seconds, counted from the server's finished protocol, not the 30-second
            // inactivity timeout. 10 seconds is that wait plus room for interruption and cleanup, and no caller cancels.
            var status = await pass.WaitAsync(TimeSpan.FromSeconds(10), ct);

            Assert.Equal(SyncFailure.Connection, status.Failure);
            Assert.True(status.HasPendingChanges);
        }
        finally
        {
            world.Server.BeforeExit = null;
            held.TrySetResult();
        }
    }

    [Fact]
    public async Task A_repository_the_host_does_not_have_is_an_error_naming_the_repository_not_an_offline_state()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        using (var clone = new Repository(a.CloneFolder))
            clone.Network.Remotes.Update("origin", r => r.Url = $"ssh://git@127.0.0.1:{world.Server.Port}/missing.git");
        a.AddTask("Pending", "Nowhere to go");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.Repository, status.Failure);
    }

    /// <summary>What a host says when it refuses the push, and the failure Sync must name: an explicit denial of the key or of write permission is Authentication; any other reason, even one that mentions read-only, is Repository.</summary>
    public static TheoryData<string, SyncFailure> Refusals => new()
    {
        { "fatal: Permission denied", SyncFailure.Authentication },
        { "ERROR: Access denied for this deploy key", SyncFailure.Authentication },
        { "fatal: ACCESS DENIED", SyncFailure.Authentication },
        { "ERROR: The key is marked as read only", SyncFailure.Authentication },
        { "ERROR: This deploy key does not have write access", SyncFailure.Authentication },
        { "ERROR: Key is marked as read-only", SyncFailure.Authentication },
        { "fatal: read-only file system", SyncFailure.Repository },
        { "fatal: the remote hung up unexpectedly", SyncFailure.Repository },
        { "fatal: repository is over its storage quota", SyncFailure.Repository },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_key_that_lost_write_access_fails_the_push_as_an_error_named_by_the_hosts_explanation_not_an_offline_state(string hostText, SyncFailure expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
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
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Setup_refuses_an_existing_clone_whose_key_the_host_will_not_let_push_and_keeps_the_hosts_explanation(string hostText, SyncFailure expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        world.Server.AllowPush = false;
        world.Server.DenialText = hostText;
        var device = world.AddDevice("a");
        Repository.Clone(world.Origin.Path, device.CloneFolder);
        await device.Sync.Setup.GenerateKeyAsync(ct);

        await TrustingAsync(device, () => device.Sync.Setup.UseExistingAsync(world.Address, device.CloneFolder, ct), ct);
        var status = device.Sync.Status;

        Assert.False(device.Sync.Setup.State.IsConfigured);
        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(expected, status.Failure);
        Assert.Contains(hostText, status.Details);
    }

    [Fact]
    public async Task Setup_does_not_take_a_command_that_exits_zero_without_advertising_anything_as_proof_of_write_access()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        world.Server.AllowPush = false;
        world.Server.RefusedExitCode = 0;
        var device = world.AddDevice("a");
        Repository.Clone(world.Origin.Path, device.CloneFolder);
        await device.Sync.Setup.GenerateKeyAsync(ct);

        await TrustingAsync(device, () => device.Sync.Setup.UseExistingAsync(world.Address, device.CloneFolder, ct), ct);
        var status = device.Sync.Status;

        // Exit zero is necessary but not enough: receive-pack must also advertise. Nothing here says the key is denied, so it is a protocol failure.
        Assert.False(device.Sync.Setup.State.IsConfigured);
        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.Repository, status.Failure);
    }

    [Fact]
    public async Task Setup_names_a_connection_failure_when_the_host_refuses_to_start_the_write_check()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        world.Server.RefuseCommand = verb => verb == "receive-pack";
        var device = world.AddDevice("a");
        Repository.Clone(world.Origin.Path, device.CloneFolder);
        await device.Sync.Setup.GenerateKeyAsync(ct);

        await TrustingAsync(device, () => device.Sync.Setup.UseExistingAsync(world.Address, device.CloneFolder, ct), ct);
        var status = device.Sync.Status;

        // Fetching worked, so the host is reachable and the key is known; a refused session is no verdict on write access.
        Assert.False(device.Sync.Setup.State.IsConfigured);
        Assert.Equal(SyncFailure.Connection, status.Failure);
        Assert.NotEqual(SyncFailure.Unexpected, status.Failure);
    }

    [Fact]
    public async Task An_existing_up_to_date_clone_with_a_read_only_key_is_not_configured_until_the_key_may_push()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        world.Server.AllowPush = false;
        world.Server.DenialText = "ERROR: The key is marked as read only";
        var device = world.AddDevice("a");
        Repository.Clone(world.Origin.Path, device.CloneFolder);

        await AssertNotConfiguredUntilTheKeyMayPushAsync(world, device, ct);
    }

    [Fact]
    public async Task An_existing_empty_clone_with_nothing_to_commit_and_a_read_only_key_is_not_configured_until_the_key_may_push()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
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
    }

    [Fact]
    public async Task A_clone_the_host_denies_write_access_keeps_that_denial_as_its_failure_when_undoing_the_clone_also_fails()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "An open file only blocks its own deletion on Windows.");
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        world.Server.AllowPush = false;
        world.Server.DenialText = "ERROR: The key is marked as read only";
        var a = world.AddDevice("a");
        await a.Sync.Setup.GenerateKeyAsync(ct);
        Assert.False(await a.TryCloneAsync(ct));
        await a.Sync.Setup.AcceptHostKeyAsync(a.Sync.Setup.State.HostKey!.Fingerprint, ct);
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
    }

    // Setup with a key the host refuses to let push must not finish, however the sync then ends; once the key may push, it finishes and the origin is as it was.
    private static async Task AssertNotConfiguredUntilTheKeyMayPushAsync(SyncWorld world, Device device, CancellationToken ct)
    {
        var originTip = world.Origin.MainTip;
        await device.Sync.Setup.GenerateKeyAsync(ct);

        await TrustingAsync(device, () => device.Sync.Setup.UseExistingAsync(world.Address, device.CloneFolder, ct), ct);
        var status = await device.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        if (status.Failure == SyncFailure.UnknownHostKey)
        {
            await device.Sync.Setup.AcceptHostKeyAsync(device.Sync.Setup.State.HostKey!.Fingerprint, ct);
            status = await device.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        }

        Assert.False(device.Sync.Setup.State.IsConfigured);
        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.Authentication, status.Failure);
        Assert.Equal(originTip, world.Origin.MainTip);

        world.Server.AllowPush = true;
        await device.Sync.Setup.UseExistingAsync(world.Address, device.CloneFolder, ct);
        var allowed = await device.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Idle, allowed.State);
        Assert.True(device.Sync.Setup.State.IsConfigured);
    }

    // Runs one setup call; when the server's host key is offered for the first time, trusts it, as the person setting up would, and runs it again.
    private static async Task TrustingAsync(Device device, Func<Task> setup, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await setup();
                return;
            }
            catch (IOException) when (attempt == 0 && device.Sync.Setup.State.HostKey is { CanAccept: true } offered)
            {
                await device.Sync.Setup.AcceptHostKeyAsync(offered.Fingerprint, ct);
            }
            catch (IOException)
            {
                // A read-only key may make setup itself fail; the sync below shows the same refusal in status.
                return;
            }
        }
    }
}
