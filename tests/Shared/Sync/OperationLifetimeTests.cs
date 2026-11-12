using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>The device key handle and transport cleanup live until the operation's work has really ended.</summary>
[Unit]
public sealed class OperationLifetimeTests
{
    private readonly SyncFixture f = new();

    [Fact]
    public async Task Opens_the_key_for_a_pass_and_releases_it_afterwards()
    {
        f.Ready();
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(1, f.Keys.OpenCalls);
        Assert.Equal(0, f.Keys.OpenHandles);
    }

    [Fact]
    public async Task Releases_the_key_when_a_pass_fails()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        held.Fail(new GitFailureException(SyncFailure.Authentication, "Denied."));
        await Record.ExceptionAsync(() => running);

        Assert.Equal(0, f.Keys.OpenHandles);
    }

    [Fact]
    public async Task Keeps_the_key_open_and_holds_the_next_operation_until_transport_cleanup_ends()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        held.HoldTransportCleanup();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        var cleanupEnded = false;
        var handleDisposedAfterCleanup = false;
        var nextStartedAfterCleanup = false;
        f.Keys.OnHandleDisposed = () => handleDisposedAfterCleanup = cleanupEnded;
        f.Git.OnUseExisting = () => nextStartedAfterCleanup = cleanupEnded;
        var next = sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);

        held.Complete();
        cleanupEnded = true;
        held.FinishCleanup();
        await running;
        await next;

        Assert.True(handleDisposedAfterCleanup);
        Assert.True(nextStartedAfterCleanup);
        Assert.Equal(0, f.Keys.OpenHandles);
    }

    [Fact]
    public async Task Waits_for_transport_cleanup_after_a_failed_pass_too()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        held.HoldTransportCleanup();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        var cleanupEnded = false;
        var handleDisposedAfterCleanup = false;
        f.Keys.OnHandleDisposed = () => handleDisposedAfterCleanup = cleanupEnded;

        held.Fail(new GitFailureException(SyncFailure.Connection, "Dropped."));
        cleanupEnded = true;
        held.FinishCleanup();
        await Record.ExceptionAsync(() => running);

        Assert.True(handleDisposedAfterCleanup);
        Assert.Equal(0, f.Keys.OpenHandles);
    }

    [Fact]
    public async Task Stopping_completes_only_after_cancelled_work_and_its_transport_cleanup_end()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        held.HoldTransportCleanup();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        var cleanupEnded = false;
        var stopCompletedAfterCleanup = false;

        var stop = sync.StopAsync(CancellationToken.None);
        var observed = stop.ContinueWith(_ => stopCompletedAfterCleanup = cleanupEnded, TaskScheduler.Default);
        await held.WasCancelled;
        held.EndCancelled();
        cleanupEnded = true;
        held.FinishCleanup();
        await stop;
        await observed;
        await Record.ExceptionAsync(() => running);

        Assert.True(stopCompletedAfterCleanup);
        Assert.Equal(0, f.Keys.OpenHandles);
    }
}
