using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>What Updates logs, by the logging design's table: each check, download, Auto update change, and restart at Information, each failure of them at Warning or Error with its exception.</summary>
[Unit]
public class UpdatesLoggingTests
{
    private static IEnumerable<LogEvent> Problems(UpdatesHarness app) => app.Log.Events.Where(e => e.Level >= LogEventLevel.Warning);

    // Waits until the first check's entries are logged, since the timer for the next night is armed only after them.
    private static async Task StartWithVersionFoundAsync(UpdatesHarness app, string version)
    {
        app.Install.Found = UpdatesHarness.Newer(version);
        await app.StartAndFirstCheckDoneAsync();
    }

    [WpfFact]
    public async Task A_check_that_finds_a_version_logs_at_Information_naming_it()
    {
        var app = new UpdatesHarness();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");

        await app.StartAndFirstCheckDoneAsync();

        app.Log.Events.SingleAt(LogEventLevel.Information, "2.2.0");
        app.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public async Task A_check_that_finds_nothing_logs_one_entry_at_Information()
    {
        var app = new UpdatesHarness();

        await app.StartAndFirstCheckDoneAsync();

        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Information));
        app.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public async Task A_failed_check_logs_a_Warning_or_Error_with_the_exception()
    {
        var app = new UpdatesHarness();
        var failure = new HttpRequestException("No network.");
        app.Install.FailChecks(failure);

        await app.StartAndFirstCheckDoneAsync();

        var entry = Assert.Single(Problems(app));
        Assert.True(entry.HasException(failure));
    }

    [WpfFact]
    public async Task Each_failed_check_logs_its_own_entry()
    {
        var app = new UpdatesHarness();
        app.Install.FailChecks(new HttpRequestException("No network."));
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Advance(TimeSpan.FromHours(3));
        await app.Install.WaitForCompletedChecksAsync(2);
        await UpdatesHarness.Pump();
        await app.Time.WaitForActiveTimerAsync();

        Assert.Equal(2, Problems(app).Count());
    }

    [WpfFact]
    public async Task A_check_that_was_asked_for_and_finds_a_version_logs_at_Information_naming_it()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");
        var before = app.Log.Events.Count;

        await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        app.Log.After(before).SingleAt(LogEventLevel.Information, "2.2.0");
        app.Log.After(before).AssertNoProblems();
    }

    [WpfFact]
    public async Task A_failed_check_that_was_asked_for_logs_one_Warning_with_the_exception()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();
        var failure = new HttpRequestException("No network.");
        app.Install.FailChecks(failure);

        await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        var entry = Assert.Single(Problems(app));
        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.True(entry.HasException(failure));
    }

    [WpfFact]
    public async Task A_failed_check_that_two_calls_share_logs_one_Warning()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();
        app.Install.HoldChecks();
        var first = app.Updates.CheckNowAsync();
        await app.Install.WaitForChecksAsync(2);
        var second = app.Updates.CheckNowAsync();
        await UpdatesHarness.Pump();

        app.Install.ReleaseChecksWithFailure(new HttpRequestException("No network."));
        await first.WaitAsync(TestWait.ForSignal);
        await second.WaitAsync(TestWait.ForSignal);

        Assert.Single(Problems(app));
    }

    [WpfFact]
    public async Task The_result_of_a_check_started_with_Check_for_updates_says_so_at_Information()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");
        var before = app.Log.Events.Count;

        await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        var entry = app.Log.After(before).SingleAt(LogEventLevel.Information, "2.2.0");
        Assert.Equal("Check for updates", entry.Property("Origin"));
    }

    [WpfFact]
    public async Task The_result_of_a_scheduled_check_says_the_schedule_started_it_at_Information()
    {
        var app = new UpdatesHarness();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");

        await app.StartAndFirstCheckDoneAsync();

        var entry = app.Log.Events.SingleAt(LogEventLevel.Information, "2.2.0");
        Assert.Equal("the schedule", entry.Property("Origin"));
    }

    [WpfFact]
    public async Task A_Check_for_updates_that_joins_a_scheduled_check_under_way_logs_at_Information_that_it_joined()
    {
        var app = new UpdatesHarness();
        app.Install.HoldChecks();
        app.Start();
        await app.Install.WaitForChecksAsync(1);

        var pending = app.Updates.CheckNowAsync();
        await UpdatesHarness.Pump();

        Assert.False(pending.IsCompleted);
        Assert.Single(app.Log.Events.At(LogEventLevel.Information), entry => Equals(entry.Property("Origin"), "Check for updates"));
        app.Install.ReleaseChecks();
        await pending.WaitAsync(TestWait.ForSignal);
    }

    [WpfFact]
    public async Task A_failed_scheduled_check_that_Check_for_updates_joined_logs_one_Warning()
    {
        var app = new UpdatesHarness();
        app.Install.HoldChecks();
        app.Start();
        await app.Install.WaitForChecksAsync(1);
        var pending = app.Updates.CheckNowAsync();
        await UpdatesHarness.Pump();

        app.Install.ReleaseChecksWithFailure(new HttpRequestException("No network."));
        await pending.WaitAsync(TestWait.ForSignal);

        Assert.Single(Problems(app));
    }

    [WpfFact]
    public async Task A_check_cancelled_by_the_stop_logs_no_problem()
    {
        var app = new UpdatesHarness();
        app.Install.HoldChecks();
        app.Start();
        await app.Install.WaitForChecksAsync(1);

        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        app.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public async Task An_update_logs_the_start_and_the_end_of_its_download_at_Information_naming_the_version()
    {
        var app = new UpdatesHarness();
        await StartWithVersionFoundAsync(app, "2.2.0");
        await UpdatesHarness.Pump();
        var before = app.Log.Events.Count;

        app.Updates.UpdateNow();
        await app.Exit.WaitForEndsAsync(1);

        var entries = app.Log.After(before);
        Assert.True(entries.At(LogEventLevel.Information, "2.2.0").Count >= 2);
        entries.AssertNoProblems();
    }

    [WpfFact]
    public async Task A_download_still_under_way_has_logged_its_start_but_not_its_end()
    {
        var app = new UpdatesHarness();
        await StartWithVersionFoundAsync(app, "2.2.0");
        await UpdatesHarness.Pump();
        var before = app.Log.Events.Count;
        app.Install.HoldDownloads();

        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);
        var whileHeld = app.Log.After(before).At(LogEventLevel.Information, "2.2.0").Count;
        app.Install.ReleaseDownloads();
        await app.Exit.WaitForEndsAsync(1);

        Assert.Equal(1, whileHeld);
        Assert.Equal(2, app.Log.After(before).At(LogEventLevel.Information, "2.2.0").Count);
    }

    [WpfFact]
    public async Task A_failed_download_logs_a_Warning_or_Error_with_the_exception_naming_the_version()
    {
        var app = new UpdatesHarness();
        await StartWithVersionFoundAsync(app, "2.2.0");
        var failure = new HttpRequestException("The download broke.");
        app.Install.FailDownloads(failure);

        app.Updates.UpdateNow();
        await app.WaitForStateAsync(state => state.Failure is not null);
        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        var entry = Assert.Single(Problems(app));
        Assert.True(entry.HasException(failure));
        Assert.True(entry.Names("2.2.0"));
    }

    [WpfFact]
    public async Task A_download_cancelled_by_the_stop_logs_no_problem()
    {
        var app = new UpdatesHarness();
        await StartWithVersionFoundAsync(app, "2.2.0");
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);

        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        app.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public async Task The_restart_into_the_new_version_logs_at_Information_naming_it()
    {
        var app = new UpdatesHarness();
        await StartWithVersionFoundAsync(app, "2.2.0");
        app.Updates.UpdateNow();
        await app.Exit.WaitForEndsAsync(1);
        await UpdatesHarness.Pump();
        var before = app.Log.Events.Count;

        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        app.Log.After(before).SingleAt(LogEventLevel.Information, "2.2.0");
        app.Log.After(before).AssertNoProblems();
    }

    [WpfFact]
    public async Task A_restart_that_fails_is_thrown_to_the_caller_and_logs_no_Warning_or_Error_since_the_platform_logs_it()
    {
        var app = new UpdatesHarness();
        await StartWithVersionFoundAsync(app, "2.2.0");
        var failure = new InvalidOperationException("The updater is missing.");
        app.Install.ApplyFailure = failure;
        app.Updates.UpdateNow();
        await app.Exit.WaitForEndsAsync(1);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Updates.StopAsync());

        Assert.Same(failure, thrown);
        Assert.DoesNotContain(app.Log.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [WpfTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Turning_Auto_update_on_or_off_logs_one_entry_at_Information(bool on)
    {
        var app = new UpdatesHarness();
        app.WriteAutoUpdateFile(on ? "off" : "on");
        await app.StartAndFirstCheckDoneAsync();
        var before = app.Log.Events.Count;

        app.Updates.SetAutoUpdate(on);

        var entries = app.Log.After(before);
        Assert.Single(entries.At(LogEventLevel.Information));
        entries.AssertNoProblems();
    }

    [WpfFact]
    public async Task An_Auto_update_file_that_cannot_be_read_logs_a_Warning_or_Error_with_the_exception()
    {
        var app = new UpdatesHarness();
        app.WriteAutoUpdateFile("on");
        app.Files.FailReadsUnder(UpdatesHarness.DeviceFolder);

        await app.StartAndFirstCheckDoneAsync();

        Assert.NotNull(Assert.Single(Problems(app)).Exception);
    }

    [WpfFact]
    public async Task An_Auto_update_file_that_is_missing_logs_no_problem()
    {
        var app = new UpdatesHarness();

        await app.StartAndFirstCheckDoneAsync();

        app.Log.Events.AssertNoProblems();
    }
}
