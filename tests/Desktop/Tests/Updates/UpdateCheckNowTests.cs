using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>
/// A check you ask for with CheckNowAsync: the same check as the scheduled one, which looks and never downloads, leaves the night
/// schedule alone, joins a check under way, does nothing while updating, and returns null or the reason it failed.
/// </summary>
[Unit]
public class UpdateCheckNowTests
{
    // Started, with the first check done and its night timer armed, so a check you ask for can't meet the first one.
    private static async Task<UpdatesHarness> StartedAsync(string? found = null)
    {
        var app = new UpdatesHarness();
        app.Install.Found = found is null ? null : UpdatesHarness.Newer(found);
        await app.StartAndFirstCheckDoneAsync();
        return app;
    }

    [WpfFact]
    public async Task A_newer_version_it_finds_becomes_available_and_it_returns_null()
    {
        var app = await StartedAsync();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");

        var reason = await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        Assert.Null(reason);
        Assert.Equal(new UpdateState(UpdateStage.Available, "2.1.0", "2.2.0", null), app.Updates.State);
    }

    [WpfFact]
    public async Task It_only_looks_and_never_downloads_or_ends_the_app()
    {
        var app = await StartedAsync();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");

        await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);
        await UpdatesHarness.Pump();

        Assert.Equal(2, app.Install.CheckCount);
        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Equal(0, app.Exit.EndCount);
    }

    [WpfFact]
    public async Task Finding_nothing_returns_null_and_clears_a_version_shown_before()
    {
        var app = await StartedAsync("2.2.0");
        app.Install.Found = null;

        var reason = await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        Assert.Null(reason);
        Assert.Equal(new UpdateState(UpdateStage.NoUpdate, "2.1.0", null, null), app.Updates.State);
    }

    [WpfFact]
    public async Task It_raises_Changed_when_it_finds_a_version_so_every_viewer_shows_it()
    {
        var app = await StartedAsync();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");
        var raised = 0;
        app.Updates.Changed += (_, _) => raised++;

        await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        Assert.True(raised > 0);
    }

    [WpfFact]
    public async Task It_does_not_move_the_night_check_which_still_runs_at_3()
    {
        var app = await StartedAsync();
        await app.AdvanceHourlyAsync(2);
        await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);
        Assert.Equal(2, app.Install.CheckCount);

        await app.AdvanceHourlyAsync(1);

        await app.Install.WaitForChecksAsync(3);
    }

    [WpfFact]
    public async Task A_failure_returns_its_reason_and_changes_nothing()
    {
        var app = await StartedAsync("2.2.0");
        var before = app.Updates.State;
        app.Install.FailChecks(new HttpRequestException("No network."));

        var reason = await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        Assert.NotNull(reason);
        Assert.Contains("No network.", reason);
        Assert.Equal(before, app.Updates.State);
    }

    [WpfFact]
    public async Task A_call_while_the_scheduled_check_runs_waits_for_it_without_a_second_request_and_reports_its_outcome()
    {
        var app = new UpdatesHarness();
        app.Install.HoldChecks();
        app.Start();
        await app.Install.WaitForChecksAsync(1);
        app.Install.Found = UpdatesHarness.Newer("2.2.0");

        var pending = app.Updates.CheckNowAsync();
        await UpdatesHarness.Pump();
        Assert.False(pending.IsCompleted);
        app.Install.ReleaseChecks();
        var reason = await pending.WaitAsync(TestWait.ForSignal);

        Assert.Null(reason);
        Assert.Equal(1, app.Install.CheckCount);
        Assert.Equal(UpdateStage.Available, app.Updates.State.Stage);
    }

    [WpfFact]
    public async Task A_call_while_the_scheduled_check_runs_reports_its_failure()
    {
        var app = new UpdatesHarness();
        app.Install.HoldChecks();
        app.Start();
        await app.Install.WaitForChecksAsync(1);
        var pending = app.Updates.CheckNowAsync();
        await UpdatesHarness.Pump();

        app.Install.ReleaseChecksWithFailure(new HttpRequestException("No network."));
        var reason = await pending.WaitAsync(TestWait.ForSignal);

        Assert.Contains("No network.", reason);
        Assert.Equal(1, app.Install.CheckCount);
    }

    [WpfFact]
    public async Task Two_calls_while_a_check_runs_make_one_request_and_both_report_its_outcome()
    {
        var app = await StartedAsync();
        app.Install.HoldChecks();
        var first = app.Updates.CheckNowAsync();
        await app.Install.WaitForChecksAsync(2);
        var second = app.Updates.CheckNowAsync();
        await UpdatesHarness.Pump();

        app.Install.ReleaseChecksWithFailure(new HttpRequestException("No network."));

        Assert.Contains("No network.", await first.WaitAsync(TestWait.ForSignal));
        Assert.Contains("No network.", await second.WaitAsync(TestWait.ForSignal));
        Assert.Equal(2, app.Install.CheckCount);
    }

    [WpfFact]
    public async Task A_call_after_a_check_ended_makes_a_new_request()
    {
        var app = await StartedAsync();
        await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        Assert.Equal(3, app.Install.CheckCount);
    }

    [WpfFact]
    public async Task While_updating_it_checks_nothing_and_returns_null()
    {
        var app = await StartedAsync("2.2.0");
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);

        var reason = await app.Updates.CheckNowAsync().WaitAsync(TestWait.ForSignal);

        Assert.Null(reason);
        Assert.Equal(1, app.Install.CheckCount);
        Assert.Equal(UpdateStage.Updating, app.Updates.State.Stage);
    }
}
