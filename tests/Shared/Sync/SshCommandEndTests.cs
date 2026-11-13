using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// How a remote command's end is judged: its exit report is the truth, a failure to send EOF is only a fault that
/// the exit report may explain within five seconds, and a caller's cancel or the network watchdog ends the wait.
/// The grace wait runs on its own fake clock, apart from the watchdog's, so a test knows which timer it advances.
/// </summary>
[Unit]
public sealed class SshCommandEndTests : IAsyncDisposable
{
    private static readonly TimeSpan NetworkLimit = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly FakeRemoteCommand command = new();
    private readonly FakeTimeProvider graceClock = new();
    private readonly FakeTimeProvider watchdogClock = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly InactivityWatchdog watchdog;

    public SshCommandEndTests()
    {
        watchdog = new InactivityWatchdog(watchdogClock, NetworkLimit, () => { });
    }

    public async ValueTask DisposeAsync()
    {
        await watchdog.DisposeAsync();
        cancellation.Dispose();
    }

    // The scope is per thread, so it is entered and left in this synchronous method; the instance stays usable afterwards.
    private OperationScope NewScope()
    {
        var keys = new FakeDeviceKeyStore();
        using var key = keys.Generate();
        using var scope = OperationScope.Enter(
            new GitConnection(key, "Laptop", (_, _, _, _) => HostKeyVerdict.Trusted, new TransportCleanup()),
            cancellation.Token,
            graceClock);
        return scope;
    }

    private SshCommandIo Begin()
    {
        var io = new SshCommandIo(command, cancellation.Token, () => { }, watchdog, NewScope());
        io.Begin();
        return io;
    }

    private async Task<GitFailureException?> Within(Task<GitFailureException?> end) =>
        await end.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

    // Starts the end, and returns once it waits on its five-second timer or has already ended, so the test advances a timer that exists.
    private async Task<Task<GitFailureException?>> StartEndAsync(SshCommandIo io)
    {
        var end = io.EndAsync();
        await Task.WhenAny(end, graceClock.WaitForActiveTimerAsync());
        return end;
    }

    // The host's exit report arrives before EndAsync starts, or late in the wait, after 4.9 of its 5 seconds.
    private async Task<GitFailureException?> EndWithReportAsync(bool late, int? status, string? signal = null, string error = "")
    {
        var io = Begin();
        if (!late)
        {
            command.Finish(status, signal, error);
            return await Within(io.EndAsync());
        }

        var end = await StartEndAsync(io);
        graceClock.Advance(TimeSpan.FromSeconds(4.9));
        command.Finish(status, signal, error);
        return await Within(end);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_no_failure_when_sending_EOF_fails_but_the_command_then_exits_with_zero(bool late)
    {
        command.EofFailure = new IOException("The channel was closed.");

        var failure = await EndWithReportAsync(late, 0);

        Assert.Null(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_an_authentication_failure_with_the_host_text_when_sending_EOF_fails_and_the_error_output_refuses_in_any_letter_case(bool late)
    {
        command.EofFailure = new IOException("The channel was closed.");

        var failure = await EndWithReportAsync(late, 1, error: "ERROR: Permission Denied");

        Assert.Equal(SyncFailure.Authentication, failure!.Kind);
        Assert.Contains("ERROR: Permission Denied", failure.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_a_repository_failure_with_the_error_output_when_sending_EOF_fails_and_the_command_exits_nonzero(bool late)
    {
        command.EofFailure = new IOException("The channel was closed.");

        var failure = await EndWithReportAsync(late, 1, error: "read-only file system");

        Assert.Equal(SyncFailure.Repository, failure!.Kind);
        Assert.Contains("read-only file system", failure.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_a_repository_failure_naming_no_exit_signal_when_sending_EOF_fails_and_the_command_ends_without_a_status(bool late)
    {
        command.EofFailure = new IOException("The channel was closed.");

        var failure = await EndWithReportAsync(late, null);

        Assert.Equal(SyncFailure.Repository, failure!.Kind);
        Assert.Contains("no exit signal", failure.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_a_repository_failure_naming_the_exit_signal_when_sending_EOF_fails_and_the_command_is_killed(bool late)
    {
        command.EofFailure = new IOException("The channel was closed.");

        var failure = await EndWithReportAsync(late, null, signal: "KILL");

        Assert.Equal(SyncFailure.Repository, failure!.Kind);
        Assert.Contains("KILL", failure.Message);
    }

    [Fact]
    public async Task Reports_a_connection_failure_when_sending_EOF_fails_and_the_command_does_not_exit_within_five_seconds()
    {
        command.EofFailure = new IOException("The channel was closed.");
        var io = Begin();
        var end = await StartEndAsync(io);

        graceClock.Advance(TimeSpan.FromSeconds(5));
        var failure = await Within(end);

        Assert.Equal(SyncFailure.Connection, failure!.Kind);
        Assert.Contains("within 5 seconds", failure.Message);
    }

    [Fact]
    public async Task Reports_no_failure_when_the_caller_cancelled_before_the_end_even_though_sending_EOF_fails()
    {
        command.EofFailure = new IOException("The channel was closed.");
        await cancellation.CancelAsync();
        var io = Begin();

        var failure = await Within(io.EndAsync());

        Assert.Null(failure);
    }

    [Fact]
    public async Task Reports_no_failure_without_waiting_out_the_five_seconds_when_the_caller_cancels_during_the_wait()
    {
        command.EofFailure = new IOException("The channel was closed.");
        var io = Begin();
        var end = await StartEndAsync(io);

        await cancellation.CancelAsync();
        var failure = await Within(end);

        Assert.Null(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_a_connection_timeout_without_a_further_wait_when_the_network_watchdog_had_already_expired(bool eofFails)
    {
        if (eofFails)
        {
            command.EofFailure = new IOException("The channel was closed.");
        }

        watchdog.Begin();
        watchdogClock.Advance(NetworkLimit);
        var io = Begin();

        var failure = await Within(io.EndAsync());

        Assert.Equal(SyncFailure.Connection, failure!.Kind);
        Assert.Contains("no data from the host", failure.Message);
    }

    [Fact]
    public async Task Reports_no_failure_when_the_command_exits_with_zero_after_EOF_was_sent()
    {
        var io = Begin();
        command.Finish(0);

        var failure = await Within(io.EndAsync());

        Assert.Null(failure);
    }

    [Fact]
    public async Task Reports_a_repository_failure_when_the_command_exits_nonzero_after_EOF_was_sent()
    {
        var io = Begin();
        command.Finish(1, error: "hook declined");

        var failure = await Within(io.EndAsync());

        Assert.Equal(SyncFailure.Repository, failure!.Kind);
        Assert.Contains("hook declined", failure.Message);
    }

    [Fact]
    public void Begins_without_failing_when_the_host_ended_the_command_before_its_input_opened()
    {
        command.Finish(0);

        var thrown = Record.Exception(() => Begin());

        Assert.Null(thrown);
    }

    [Fact]
    public async Task Reports_no_failure_when_the_host_ended_the_command_with_zero_before_its_input_opened()
    {
        command.Finish(0);
        var io = Begin();

        var failure = await Within(io.EndAsync());

        Assert.Null(failure);
    }

    [Theory]
    [InlineData("fatal: Permission denied (publickey).")]
    [InlineData("ERROR: The key you are authenticating with has been marked as read only.")]
    public async Task Reports_an_authentication_failure_with_the_host_text_when_the_host_refused_and_ended_the_command_before_its_input_opened(string error)
    {
        command.Finish(128, error: error);
        var io = Begin();

        var failure = await Within(io.EndAsync());

        Assert.Equal(SyncFailure.Authentication, failure!.Kind);
        Assert.Contains(error, failure.Message);
    }

    [Fact]
    public async Task Reports_a_repository_failure_with_the_host_text_when_the_host_ended_the_command_with_another_error_before_its_input_opened()
    {
        command.Finish(128, error: "fatal: 'me/tasks.git' does not appear to be a git repository");
        var io = Begin();

        var failure = await Within(io.EndAsync());

        Assert.Equal(SyncFailure.Repository, failure!.Kind);
        Assert.Contains("does not appear to be a git repository", failure.Message);
    }

    [Fact]
    public void Reads_what_the_host_printed_before_ending_the_command_then_the_end_of_output_and_drops_writes()
    {
        command.Print([1, 2, 3]);
        command.Finish(0);
        var io = Begin();
        var buffer = new byte[16];

        var first = io.Read(buffer, buffer.Length);
        var second = io.Read(buffer, buffer.Length);
        var thrown = Record.Exception(() => io.Write([4, 5], 2));

        Assert.Equal(3, first);
        Assert.Equal(0, second);
        Assert.Null(thrown);
    }
}
