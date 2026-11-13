using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

[Integration]
public sealed class TransportIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    // Shorter than the 30 second connection-stage timeout, so only the caller's cancellation can end a stalled channel open in time.
    private static readonly TimeSpan ChannelOpenBound = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task An_unknown_host_key_is_offered_with_the_servers_real_fingerprint_and_trusting_it_lets_the_clone_finish()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        var a = world.AddDevice("a");
        await a.Sync.Setup.GenerateKeyAsync(ct);

        Assert.False(await a.TryCloneAsync(ct));
        var offered = a.Sync.Setup.State.HostKey!;
        Assert.Equal(world.Server.HostKeyFingerprint, offered.Fingerprint);
        Assert.True(offered.CanAccept);

        await a.Sync.Setup.AcceptHostKeyAsync(offered.Fingerprint, ct);
        Assert.True(await a.TryCloneAsync(ct));
    }

    [Fact]
    public async Task A_host_key_that_differs_from_the_trusted_one_is_refused_and_cannot_be_accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        var a = world.AddDevice("a");
        await a.Sync.Setup.GenerateKeyAsync(ct);
        Directory.CreateDirectory(a.DeviceFolder);
        await File.WriteAllTextAsync(
            Path.Combine(a.DeviceFolder, "known-hosts.json"),
            $$"""{"version":1,"hosts":[{"host":"127.0.0.1","port":{{world.Server.Port}},"fingerprint":"SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}]}""",
            ct);

        await Assert.ThrowsAnyAsync<Exception>(() => a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, ct));
        Assert.False(a.Sync.Setup.State.HostKey?.CanAccept ?? false);
        Assert.False(a.Sync.Setup.State.IsConfigured);
    }

    [Fact]
    public async Task A_key_the_server_does_not_know_fails_authentication_and_leaves_setup_unfinished()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(authorizedKey: SyncWorld.StrangerKey());
        var a = world.AddDevice("a");
        await a.Sync.Setup.GenerateKeyAsync(ct);
        Assert.False(await a.TryCloneAsync(ct));
        await a.Sync.Setup.AcceptHostKeyAsync(a.Sync.Setup.State.HostKey!.Fingerprint, ct);

        await Assert.ThrowsAnyAsync<Exception>(() => a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, ct));

        Assert.False(a.Sync.Setup.State.IsConfigured);
        Assert.Null(a.Sync.Setup.State.RepositoryFolder);
    }

    [Fact]
    public async Task A_saved_host_trust_that_cannot_be_read_fails_setup_as_local_storage_and_nothing_can_be_trusted_meanwhile()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "An open file only blocks reading it on Windows.");
        var ct = TestContext.Current.CancellationToken;
        const string Saved = """{"version":1,"hosts":[{"host":"other.example.org","port":22,"fingerprint":"SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}]}""";
        await using var world = await SyncWorld.StartAsync();
        var a = world.AddDevice("a");
        await a.Sync.Setup.GenerateKeyAsync(ct);
        var trustFile = Path.Combine(a.DeviceFolder, "known-hosts.json");
        Directory.CreateDirectory(a.DeviceFolder);
        await File.WriteAllTextAsync(trustFile, Saved, ct);

        // A scanner or another process holding the file makes reading it fail, which is not the host's doing.
        using (new FileStream(trustFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, ct).WaitAsync(Bound, ct));

            Assert.Equal(SyncState.Error, a.Sync.Status.State);
            Assert.Equal(SyncFailure.LocalStorage, a.Sync.Status.Failure);
            Assert.False(a.Sync.Setup.State.HostKey?.CanAccept ?? false);
            Assert.False(a.Sync.Setup.State.IsConfigured);
        }

        Assert.Equal(Saved, await File.ReadAllTextAsync(trustFile, ct));
    }

    [Fact]
    public async Task An_unreadable_file_from_another_device_is_reported_skipped_and_left_untouched_everywhere()
    {
        var ct = TestContext.Current.CancellationToken;
        const string Bad = "this is not a task";
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["tasks/bad.json"] = Bad }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var task = a.AddTask("Good", "Next to a bad file");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Contains(status.SkippedFiles, f => f.RelativePath.EndsWith("bad.json", StringComparison.Ordinal));
        Assert.Equal(Bad, await File.ReadAllTextAsync(Path.Combine(a.CloneFolder, "tasks", "bad.json"), ct));
        Assert.Equal(Bad, world.Origin.ReadOnMain("tasks/bad.json"));
        Assert.NotNull(world.Origin.ReadOnMain($"tasks/{task.Id}.json"));
    }

    [Fact]
    public async Task Saving_a_task_does_not_wait_for_a_stalled_network_operation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Server.BeforeCommand = async (_, _) =>
        {
            arrived.TrySetResult();
            await release.Task;
        };
        var pass = a.Sync.SyncNowAsync(ct);
        try
        {
            await arrived.Task.WaitAsync(Bound, ct);

            var saved = await System.Threading.Tasks.Task.Run(() => a.AddTask("While stalled", "Saved at once"), ct).WaitAsync(Bound, ct);

            Assert.Equal("While stalled", saved.Title);
        }
        finally
        {
            release.TrySetResult();
            await pass.WaitAsync(Bound, ct);
        }
    }

    [Fact]
    public async Task Cancelling_a_stalled_pass_and_stopping_leaves_the_clone_unchanged_and_completes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        a.AddTask("Pending", "Local only");
        var before = a.Snapshot();
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Server.BeforeCommand = async (_, _) =>
        {
            arrived.TrySetResult();
            await release.Task;
        };
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pass = a.Sync.SyncNowAsync(cancel.Token);
        try
        {
            await arrived.Task.WaitAsync(Bound, ct);
            await cancel.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pass.WaitAsync(Bound, ct));

            await a.Sync.StopAsync(ct).WaitAsync(Bound, ct);
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.Equal(before, a.Snapshot());
        Assert.Null(world.Origin.MainTip);
    }

    [Fact]
    public Task Cancelling_a_clone_stalled_during_setup_ends_the_clone_and_stopping_joins_its_cleanup() =>
        SyncWorld.RunAsync(StalledSetupCloneScenario);

    private static async Task StalledSetupCloneScenario(SyncWorld world)
    {
        var ct = TestContext.Current.CancellationToken;
        var a = world.AddDevice("a");
        await a.Sync.Setup.GenerateKeyAsync(ct);
        Assert.False(await a.TryCloneAsync(ct));
        await a.Sync.Setup.AcceptHostKeyAsync(a.Sync.Setup.State.HostKey!.Fingerprint, ct);
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Server.BeforeCommand = async (_, _) =>
        {
            arrived.TrySetResult();
            await release.Task;
        };
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var clone = a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, cancel.Token);
        try
        {
            await arrived.Task.WaitAsync(Bound, ct);

            // The caller's own token ends the stalled clone; the server is still holding the command.
            await cancel.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clone.WaitAsync(Bound, ct));

            await a.Sync.StopAsync(ct).WaitAsync(Bound, ct);
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.False(a.Sync.Setup.State.IsConfigured);
    }

    [Fact]
    public Task A_clone_cancelled_while_the_host_holds_it_reports_the_cancellation_with_the_cleanup_failure_when_undoing_the_clone_also_fails() =>
        SyncWorld.RunAsync(CancelledCloneWithFailingCleanupScenario, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));

    private static async Task CancelledCloneWithFailingCleanupScenario(SyncWorld world)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "An open file only blocks its own deletion on Windows.");
        var ct = TestContext.Current.CancellationToken;
        var a = world.AddDevice("a");
        await a.Sync.Setup.GenerateKeyAsync(ct);
        Assert.False(await a.TryCloneAsync(ct));
        await a.Sync.Setup.AcceptHostKeyAsync(a.Sync.Setup.State.HostKey!.Fingerprint, ct);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FileStream? held = null;

        // The write check is the clone's last remote step, so the clone's files exist when git asks for receive-pack. The server
        // then locks one file as a scanner would, cancels the caller, and holds the command, so the clone ends by cancellation
        // and undoing it fails on the locked file.
        world.Server.BeforeCommand = async (verb, _) =>
        {
            if (verb != "receive-pack")
                return;
            held = new FileStream(Path.Combine(a.CloneFolder, "held.tmp"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            await cancel.CancelAsync();
            await release.Task;
        };
        try
        {
            var failure = await Assert.ThrowsAnyAsync<IOException>(() => a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, cancel.Token).WaitAsync(Bound, ct));

            Assert.NotNull(held);
            var inner = Assert.IsType<AggregateException>(failure.InnerException);
            Assert.Contains(inner.Flatten().InnerExceptions, e => e is OperationCanceledException);
            Assert.Contains(inner.Flatten().InnerExceptions, e => e is IOException);
            Assert.Equal(SyncState.Error, a.Sync.Status.State);
            Assert.Equal(SyncFailure.LocalStorage, a.Sync.Status.Failure);
            Assert.Contains("may remain", a.Sync.Status.Details, StringComparison.OrdinalIgnoreCase);
            Assert.False(a.Sync.Setup.State.IsConfigured);
        }
        finally
        {
            release.TrySetResult();
            held?.Dispose();
        }
    }

    [Fact]
    public Task A_clone_cancelled_while_the_host_has_not_yet_opened_the_command_channel_ends_as_cancelled_within_the_bound() =>
        SyncWorld.RunAsync(CancelledWhileOpeningChannelScenario);

    private static async Task CancelledWhileOpeningChannelScenario(SyncWorld world)
    {
        var ct = TestContext.Current.CancellationToken;
        var a = world.AddDevice("a");
        await a.Sync.Setup.GenerateKeyAsync(ct);
        Assert.False(await a.TryCloneAsync(ct));
        await a.Sync.Setup.AcceptHostKeyAsync(a.Sync.Setup.State.HostKey!.Fingerprint, ct);
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Server.BeforeChannelOpen = async () =>
        {
            arrived.TrySetResult();
            await release.Task;
        };
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var clone = a.Sync.Setup.CloneAsync(world.Address, a.CloneFolder, cancel.Token);
        try
        {
            await arrived.Task.WaitAsync(Bound, ct);
            await cancel.CancelAsync();

            // The owner says cancellation interrupts the synchronous channel open; the stall is never released before the clone ends.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clone.WaitAsync(ChannelOpenBound, ct));
            await a.Sync.StopAsync(ct).WaitAsync(ChannelOpenBound, ct);
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.False(a.Sync.Setup.State.IsConfigured);
    }

    [Fact]
    public Task Cancelling_a_pass_whose_protocol_finished_but_whose_command_has_not_exited_ends_it_as_cancelled_never_as_success() =>
        SyncWorld.RunAsync(CancelDuringCleanupScenario, path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));

    private static async Task CancelDuringCleanupScenario(SyncWorld world)
    {
        var ct = TestContext.Current.CancellationToken;
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Server.BeforeExit = _ =>
        {
            finished.TrySetResult();
            return release.Task;
        };
        a.AddTask("Pending", "Cancelled while the command's exit is awaited");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pass = a.Sync.SyncNowAsync(cancel.Token);
        try
        {
            // The server has sent all it has and holds the exit status, so the pass is waiting for the command to end. The fixture's
            // clock is frozen, so no timeout can end that wait: only the caller's cancellation can.
            await finished.Task.WaitAsync(Bound, ct);
            await cancel.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pass.WaitAsync(Bound, ct));
        }
        finally
        {
            world.Server.BeforeExit = null;
            release.TrySetResult();
        }
    }
}
