using System.Security.Cryptography;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// What Sync logs: each pass's state changes, each skipped file with its path and reason, each failure with its kind
/// and cause, and each setup step. Entries are checked by level and the facts their message names, never by wording.
/// </summary>
[Unit]
public sealed class SyncLoggingTests
{
    private readonly SyncFixture f = new();

    private static Task<ISyncStatus> Returned(Task<ISyncStatus> running) =>
        running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

    private async Task<IReadOnlyList<LogEvent>> FailedPassAsync(Exception failure)
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        held.Fail(failure);
        await Returned(running);

        return f.Logs.After(before);
    }

    private static string Fingerprint(byte[] blob) => "SHA256:" + Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');

    // Passes

    [Theory]
    [InlineData(SyncState.Working)]
    [InlineData(SyncState.Idle)]
    public async Task Logs_each_state_a_successful_pass_moves_through(SyncState state)
    {
        f.Ready();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.SyncNowAsync(CancellationToken.None);

        f.Logs.After(before).SingleAt(LogEventLevel.Information, state.ToString());
        f.Logs.After(before).AssertNoProblems();
    }

    [Fact]
    public async Task Logs_a_warning_for_each_file_the_merge_could_not_combine_naming_its_path_and_reason()
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(false)
        {
            Skipped = [new FakeSkippedFile("tasks/a.json", "Both sides changed it."), new FakeSkippedFile("tasks/b.json", "Cut off.")],
        };
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.SyncNowAsync(CancellationToken.None);

        var entries = f.Logs.After(before);
        entries.SingleAt(LogEventLevel.Warning, "tasks/a.json", "Both sides changed it.");
        entries.SingleAt(LogEventLevel.Warning, "tasks/b.json", "Cut off.");
    }

    [Fact]
    public async Task Logs_the_invalid_file_failure_as_one_error_naming_its_kind()
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(false) { Skipped = [new FakeSkippedFile("tasks/a.json", "Unreadable.")] };
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.SyncNowAsync(CancellationToken.None);

        f.Logs.After(before).SingleAt(LogEventLevel.Error, SyncFailure.InvalidFile.ToString());
    }

    [Fact]
    public async Task Logs_a_lost_connection_as_a_warning_and_not_as_an_error()
    {
        var entries = await FailedPassAsync(new GitFailureException(SyncFailure.Connection, "Offline."));

        entries.SingleAt(LogEventLevel.Warning, SyncFailure.Connection.ToString());
        Assert.DoesNotContain(entries, e => e.Level >= LogEventLevel.Error);
    }

    [Theory]
    [InlineData(SyncFailure.Authentication)]
    [InlineData(SyncFailure.UnknownHostKey)]
    [InlineData(SyncFailure.ChangedHostKey)]
    [InlineData(SyncFailure.Repository)]
    [InlineData(SyncFailure.PushRejected)]
    public async Task Logs_other_failed_passes_as_one_error_naming_the_kind_and_the_cause_and_carrying_the_exception(SyncFailure kind)
    {
        var cause = new GitFailureException(kind, $"Cause text for {kind}.");

        var entries = await FailedPassAsync(cause);

        var error = entries.SingleAt(LogEventLevel.Error, kind.ToString(), cause.Message);
        Assert.True(error.HasException(cause));
        Assert.Single(entries, e => e.Level >= LogEventLevel.Error);
    }

    [Fact]
    public async Task Logs_an_unexpected_failure_as_one_error_carrying_the_exception()
    {
        var cause = new NotSupportedException("Something nobody planned for.");

        var entries = await FailedPassAsync(cause);

        var error = entries.SingleAt(LogEventLevel.Error, SyncFailure.Unexpected.ToString(), cause.Message);
        Assert.True(error.HasException(cause));
    }

    [Fact]
    public async Task Logs_nothing_at_warning_or_above_when_stopping_cancels_a_running_pass()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        var before = f.Logs.Events.Count;

        var stop = sync.StopAsync(CancellationToken.None);
        await held.WasCancelled;
        held.EndCancelled();
        await stop.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken));

        f.Logs.After(before).AssertNoProblems();
    }

    // Pass start and end

    private static LogEvent[] Starts(IEnumerable<LogEvent> entries) => [.. entries.Where(e => e.Property("Trigger") is not null)];

    private static LogEvent[] Ends(IEnumerable<LogEvent> entries) => [.. entries.Where(e => e.Property("DurationMs") is not null)];

    private static string[] TriggersOf(IEnumerable<LogEvent> entries) => [.. Starts(entries).Select(e => e.Property("Trigger")!.ToString()!)];

    [Fact]
    public async Task Logs_one_start_and_one_end_for_a_pass_at_information_start_first()
    {
        f.Ready();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.SyncNowAsync(CancellationToken.None);

        var entries = f.Logs.After(before);
        var start = Assert.Single(Starts(entries));
        var end = Assert.Single(Ends(entries));
        Assert.Equal(LogEventLevel.Information, start.Level);
        Assert.Equal(LogEventLevel.Information, end.Level);
        Assert.True(entries.ToList().IndexOf(start) < entries.ToList().IndexOf(end));
    }

    [Fact]
    public async Task Logs_the_start_of_a_manual_sync_with_the_trigger_Manual()
    {
        f.Ready();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(["Manual"], TriggersOf(f.Logs.After(before)));
    }

    [Fact]
    public async Task Logs_the_start_of_the_initial_pass_with_the_trigger_Start()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        sync.Start();
        await held.HasStarted;

        Assert.Equal(["Start"], TriggersOf(f.Logs.After(before)));
        held.Complete();
    }

    [Fact]
    public async Task Logs_the_start_of_a_pass_after_a_saved_edit_with_the_trigger_Edit()
    {
        await using var sync = await f.StartedAndSettledAsync();
        var next = f.Git.ScriptPass();
        var before = f.Logs.Events.Count;

        f.Tasks.RaiseLocalSaved();
        await f.Time.WaitForActiveTimerAsync();
        f.Time.Advance(TimeSpan.FromSeconds(5));
        await next.HasStarted;
        next.Complete();

        Assert.Equal(["Edit"], TriggersOf(f.Logs.After(before)));
    }

    [Fact]
    public async Task Logs_the_start_of_a_pass_after_a_failed_one_with_the_trigger_Retry()
    {
        f.Ready();
        var failing = f.Git.ScriptPass();
        await using var sync = f.Create();
        sync.Start();
        await failing.HasStarted;
        var next = f.Git.ScriptPass();
        var before = f.Logs.Events.Count;

        failing.Fail(new GitFailureException(SyncFailure.Connection, "Offline."));
        await f.Time.WaitForActiveTimerAsync();
        f.Time.Advance(TimeSpan.FromSeconds(30));
        await next.HasStarted;

        Assert.Equal(["Retry"], TriggersOf(f.Logs.After(before)));
        next.Complete();
    }

    [Fact]
    public async Task Logs_the_start_of_a_pass_after_the_periodic_interval_with_the_trigger_Periodic()
    {
        var interval = TimeSpan.FromMinutes(10);
        await using var sync = await f.StartedAndSettledAsync(interval);
        var next = f.Git.ScriptPass();
        var before = f.Logs.Events.Count;

        await f.Time.WaitForActiveTimerAsync();
        f.Time.Advance(interval);
        await next.HasStarted;

        Assert.Equal(["Periodic"], TriggersOf(f.Logs.After(before)));
        next.Complete();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public async Task Logs_the_end_of_a_pass_with_what_it_fetched_merged_and_pushed(bool fetched, bool merged, bool pushed)
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        held.Complete(new GitPassResult(false) { Fetched = fetched, Merged = merged, Pushed = pushed });
        await Returned(running);

        var end = Assert.Single(Ends(f.Logs.After(before)));
        Assert.Equal(LogEventLevel.Information, end.Level);
        Assert.Equal(fetched, end.Property("Fetched"));
        Assert.Equal(merged, end.Property("Merged"));
        Assert.Equal(pushed, end.Property("Pushed"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Logs_the_end_of_a_pass_with_the_count_of_files_the_merge_skipped(int count)
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(false)
        {
            Skipped = [.. Enumerable.Range(0, count).Select(i => new FakeSkippedFile($"tasks/{i}.json", "Cut off."))],
        };
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.SyncNowAsync(CancellationToken.None);

        var end = Assert.Single(Ends(f.Logs.After(before)));
        Assert.Equal(count, Convert.ToInt32(end.Property("Skipped")));
    }

    [Fact]
    public async Task Logs_the_end_of_a_pass_with_how_long_the_pass_took_in_milliseconds()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        f.Time.Advance(TimeSpan.FromMilliseconds(1234));
        held.Complete();
        await Returned(running);

        var end = Assert.Single(Ends(f.Logs.After(before)));
        Assert.Equal(1234, Convert.ToDouble(end.Property("DurationMs")));
    }

    // A pass that fails still logs its end, after its failure, with only how long it took.

    private static void AssertEndsAfterFailureWithOnlyTheDuration(IReadOnlyList<LogEvent> entries)
    {
        var end = Assert.Single(Ends(entries));
        var failure = Assert.Single(entries, e => e.Level >= LogEventLevel.Warning);
        Assert.True(entries.ToList().IndexOf(end) > entries.ToList().IndexOf(failure), "The end was logged before the failure.");
        Assert.Equal(LogEventLevel.Information, end.Level);
        Assert.Null(end.Property("Fetched"));
        Assert.Null(end.Property("Merged"));
        Assert.Null(end.Property("Pushed"));
        Assert.Null(end.Property("Skipped"));
    }

    [Fact]
    public async Task Logs_the_end_of_a_pass_that_failed_offline_after_its_failure_with_only_the_duration()
    {
        var entries = await FailedPassAsync(new GitFailureException(SyncFailure.Connection, "Offline."));

        AssertEndsAfterFailureWithOnlyTheDuration(entries);
    }

    [Fact]
    public async Task Logs_the_end_of_a_pass_that_failed_writing_the_receipt_after_its_failure_with_only_the_duration()
    {
        f.Ready();
        f.Git.NextWriteReceiptFailure = new IOException("The receipt can't be written.");
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        held.Complete(new GitPassResult(false) { Fetched = true, Merged = true, Pushed = true });
        await Returned(running);

        AssertEndsAfterFailureWithOnlyTheDuration(f.Logs.After(before));
    }

    // A failure that is thrown on is logged only by the code that ends the journey, never by the one that throws it on.

    [Fact]
    public async Task Logs_no_Warning_or_Error_for_a_manual_sync_the_selected_clone_containing_the_device_folder_refuses()
    {
        f.Keys.SeedKey();
        f.Tasks.RepositoryFolder = Path.GetDirectoryName(f.DeviceFolder)!;
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.SyncNowAsync(CancellationToken.None));

        f.Logs.After(before).AssertNoProblems();
    }

    [Fact]
    public async Task Logs_one_Error_naming_the_Repository_kind_when_the_start_finds_the_selected_clone_containing_the_device_folder()
    {
        f.Keys.SeedKey();
        f.Tasks.RepositoryFolder = Path.GetDirectoryName(f.DeviceFolder)!;
        await using var sync = f.Create();
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is not null)
                reported.TrySetResult();
        };

        sync.Start();
        await reported.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Single(f.Logs.Events, e => e.Level >= LogEventLevel.Warning && e.Names(nameof(SyncFailure.Repository)));
    }

    // Setup steps

    [Fact]
    public async Task Logs_no_Warning_or_Error_for_a_clone_that_fails_since_the_caller_that_shows_it_logs_it()
    {
        f.WithKey();
        f.Git.NextCloneFailure = new GitFailureException(SyncFailure.Connection, "Offline.");
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        f.Logs.After(before).AssertNoProblems();
    }

    [Fact]
    public async Task Logs_no_Warning_or_Error_for_using_an_existing_clone_that_fails_since_the_caller_that_shows_it_logs_it()
    {
        f.WithKey();
        f.Git.NextUseExistingFailure = new GitFailureException(SyncFailure.Repository, "Not a clone.");
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        f.Logs.After(before).AssertNoProblems();
    }

    [Fact]
    public async Task Logs_the_failure_to_refresh_the_setup_after_a_failed_step_at_Warning_or_above_with_its_exception()
    {
        f.WithKey();
        var refreshFailure = new IOException("The key can't be opened.");
        f.Keys.OpenFailure = refreshFailure;
        f.Git.NextUseExistingFailure = new GitFailureException(SyncFailure.Repository, "Not a clone.");
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        Assert.Single(f.Logs.After(before), e => e.Level >= LogEventLevel.Warning && e.HasException(refreshFailure));
    }

    public static TheoryData<Exception> KeysThatCannotBeOpened => new()
    {
        new CryptographicException("The key can't be decrypted."),
        new IOException("The key can't be read."),
        new InvalidDataException("The key is malformed."),
        new FormatException("The key is malformed."),
    };

    [Theory]
    [MemberData(nameof(KeysThatCannotBeOpened))]
    public async Task Forgets_a_key_that_cannot_be_opened_logging_the_forget_without_blocking_the_deletion(Exception openFailure)
    {
        f.WithKey();
        f.Keys.OpenFailure = openFailure;
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        Assert.False(f.Keys.HasKey);
        var entries = f.Logs.After(before);
        Assert.Single(entries, e => e.Level == LogEventLevel.Information);
        Assert.Single(entries, e => e.Level >= LogEventLevel.Warning && e.HasException(openFailure));
    }

    [Fact]
    public async Task Logs_generating_a_key_naming_its_fingerprint()
    {
        await using var sync = f.Create();

        await sync.Setup.GenerateKeyAsync(CancellationToken.None);

        f.Logs.Events.SingleAt(LogEventLevel.Information, Fingerprint(f.Keys.PublicKeyBlob));
    }

    [Fact]
    public async Task Logs_no_new_key_when_the_device_already_has_one()
    {
        f.WithKey();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.Setup.GenerateKeyAsync(CancellationToken.None);

        Assert.DoesNotContain(f.Logs.After(before), e => e.Names(Fingerprint(f.Keys.PublicKeyBlob)));
    }

    [Fact]
    public async Task Logs_forgetting_the_key_naming_its_fingerprint()
    {
        f.WithKey();
        var fingerprint = Fingerprint(f.Keys.PublicKeyBlob);
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.Setup.ForgetKeyAsync(CancellationToken.None);

        f.Logs.After(before).SingleAt(LogEventLevel.Information, fingerprint);
    }

    [Fact]
    public async Task Logs_setting_the_device_name_naming_the_new_name()
    {
        f.WithKey();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.Setup.SetDeviceNameAsync("Desk-Machine", CancellationToken.None);

        f.Logs.After(before).SingleAt(LogEventLevel.Information, "Desk-Machine");
    }

    [Fact]
    public async Task Logs_accepting_a_host_key_naming_the_host_and_fingerprint()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));
        var before = f.Logs.Events.Count;

        await sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);

        f.Logs.After(before).SingleAt(LogEventLevel.Information, f.Address.Host, "SHA256:first");
    }

    [Fact]
    public async Task Logs_cloning_naming_the_address_and_folder()
    {
        f.WithKey();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);

        f.Logs.After(before).SingleAt(LogEventLevel.Information, SyncFixture.ScpAddress, f.CloneFolder);
    }

    [Fact]
    public async Task Logs_using_an_existing_clone_naming_the_address_and_folder()
    {
        f.WithKey();
        await using var sync = f.Create();
        var before = f.Logs.Events.Count;

        await sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);

        f.Logs.After(before).SingleAt(LogEventLevel.Information, SyncFixture.ScpAddress, f.CloneFolder);
    }
}
