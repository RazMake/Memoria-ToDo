using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// The write-access check passes when receive-pack advertised and exited 0, however its input ended, and fails with
/// the host's own explanation when it exits with an error. The exit report is the truth, as for any remote command.
/// A failure while closing the connection is reported too, unless the host's explanation came first.
/// </summary>
[Unit]
public sealed class WriteAccessCheckTests
{
    private static readonly byte[] Advertisement = "000eadvertisement"u8.ToArray();

    private readonly FakeRemoteCommand command = new();
    private readonly FakeRemoteHost host;
    private readonly TransportCleanup cleanup = new();
    private readonly FakeTimeProvider clock = new();

    public WriteAccessCheckTests() => host = new FakeRemoteHost(command);

    // The scope is per thread, so it is entered and left in this synchronous method; the scope and what Run threw outlive it.
    private (Exception? Thrown, OperationScope Scope) RunCheck()
    {
        var keys = new FakeDeviceKeyStore();
        using var key = keys.Generate();
        using var scope = OperationScope.Enter(
            new GitConnection(key, "Laptop", (_, _, _, _) => HostKeyVerdict.Trusted, cleanup),
            CancellationToken.None,
            clock);
        var link = new SshLink(host, scope);

        var thrown = Record.Exception(() => WriteAccessCheck.Run(link, "me/tasks.git"));
        return (thrown, scope);
    }

    // Cleanup judges the exit report after the check returns, so the failure is read once it has finished.
    private async Task<(Exception? Thrown, GitFailureException? Failure)> CheckAsync()
    {
        var (thrown, scope) = RunCheck();
        await cleanup.WhenAllAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        return (thrown, scope.Failure);
    }

    [Fact]
    public async Task Passes_when_sending_EOF_fails_but_receive_pack_advertised_and_exited_cleanly()
    {
        command.Print(Advertisement);
        command.EofFailure = new IOException("The channel was closed.");
        command.FinishOnEof(0);

        var (thrown, failure) = await CheckAsync();

        Assert.Null(failure);
        Assert.Null(thrown);
    }

    [Fact]
    public async Task Fails_with_the_hosts_explanation_when_receive_pack_exits_with_an_error_after_sending_EOF_failed()
    {
        command.Print(Advertisement);
        command.EofFailure = new IOException("The channel was closed.");
        command.FinishOnEof(128, error: "fatal: unable to write");

        var (_, failure) = await CheckAsync();

        Assert.Equal(SyncFailure.Repository, failure!.Kind);
        Assert.Contains("fatal: unable to write", failure.Message);
    }

    [Fact]
    public async Task Fails_when_closing_the_connection_fails_after_receive_pack_exited_cleanly()
    {
        command.Print(Advertisement);
        command.FinishOnEof(0);
        host.DisposeFailure = new IOException("The connection was reset while closing.");

        var (thrown, failure) = await CheckAsync();

        Assert.Null(thrown);
        Assert.NotNull(failure);
        Assert.Equal(SyncFailure.Connection, failure.Kind);
        Assert.Contains("The connection was reset while closing.", failure.Message);
    }

    [Fact]
    public async Task Keeps_the_hosts_explanation_when_closing_the_connection_also_fails()
    {
        command.Print(Advertisement);
        command.FinishOnEof(128, error: "fatal: unable to write");
        host.DisposeFailure = new IOException("The connection was reset while closing.");

        var (_, failure) = await CheckAsync();

        Assert.Equal(SyncFailure.Repository, failure!.Kind);
        Assert.Contains("fatal: unable to write", failure.Message);
    }

    [Fact]
    public async Task Fails_with_the_hosts_refusal_when_the_host_refused_the_key_and_ended_receive_pack_before_its_input_opened()
    {
        command.Finish(128, error: "ERROR: The key you are authenticating with has been marked as read only.");

        var (thrown, failure) = await CheckAsync();

        Assert.Null(thrown);
        Assert.Equal(SyncFailure.Authentication, failure!.Kind);
        Assert.Contains("marked as read only", failure.Message);
    }

    [Fact]
    public async Task Fails_with_the_advertised_nothing_failure_when_receive_pack_exited_cleanly_before_its_input_opened()
    {
        command.Finish(0);

        var (thrown, failure) = await CheckAsync();

        Assert.Null(thrown);
        Assert.Equal(SyncFailure.Repository, failure!.Kind);
        Assert.Contains("advertised nothing", failure.Message);
    }

    [Fact]
    public async Task Fails_with_a_connection_failure_when_the_session_drops_during_the_check()
    {
        var dropped = new IOException("The connection was reset.");
        command.ReadFailure = dropped;
        command.Fault(dropped);

        var (thrown, failure) = await CheckAsync();

        Assert.Same(dropped, thrown);
        Assert.Equal(SyncFailure.Connection, failure!.Kind);
        Assert.Contains("The connection was reset.", failure.Message);
    }
}
