using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>What a check does to the state: a found version shows, a later none clears it, and a failed check changes nothing.</summary>
[Unit]
public class UpdateCheckResultTests
{
    private static readonly TimeSpan FirstNight = TimeSpan.FromHours(3);

    [WpfFact]
    public async Task Starts_with_no_update_on_the_running_version()
    {
        var app = new UpdatesHarness();

        await app.StartAndFirstCheckDoneAsync();

        Assert.Equal(new UpdateState(UpdateStage.NoUpdate, "2.1.0", null, null), app.Updates.State);
        Assert.False(app.Updates.AutoUpdate);
    }

    [WpfFact]
    public async Task A_check_that_finds_a_version_makes_it_available()
    {
        var app = new UpdatesHarness();

        await app.StartWithVersionAvailableAsync("2.2.0");

        Assert.Equal(new UpdateState(UpdateStage.Available, "2.1.0", "2.2.0", null), app.Updates.State);
    }

    [WpfFact]
    public async Task A_check_never_downloads_or_ends_the_app()
    {
        var app = new UpdatesHarness();

        await app.StartWithVersionAvailableAsync();
        await UpdatesHarness.Pump();

        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Equal(0, app.Exit.EndCount);
    }

    [WpfFact]
    public async Task A_later_check_that_finds_none_returns_to_no_update()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        await app.Time.WaitForActiveTimerAsync();
        app.Install.Found = null;

        app.Time.Advance(FirstNight);

        var state = await app.WaitForStateAsync(s => s.Stage == UpdateStage.NoUpdate);
        Assert.Equal(new UpdateState(UpdateStage.NoUpdate, "2.1.0", null, null), state);
    }

    [WpfFact]
    public async Task A_later_check_that_finds_a_newer_version_replaces_the_one_shown()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync("2.2.0");
        await app.Time.WaitForActiveTimerAsync();
        app.Install.Found = UpdatesHarness.Newer("2.3.0");

        app.Time.Advance(FirstNight);

        var state = await app.WaitForStateAsync(s => s.NewVersion == "2.3.0");
        Assert.Equal(UpdateStage.Available, state.Stage);
    }

    [WpfFact]
    public async Task A_failed_check_changes_nothing_and_shows_no_failure()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        await app.Time.WaitForActiveTimerAsync();
        var before = app.Updates.State;
        app.Install.FailChecks(new HttpRequestException("No network."));

        app.Time.Advance(FirstNight);
        await app.Install.WaitForCompletedChecksAsync(2);
        await UpdatesHarness.Pump();

        Assert.Equal(before, app.Updates.State);
    }

    [WpfFact]
    public async Task A_failed_first_check_leaves_no_update_with_no_failure()
    {
        var app = new UpdatesHarness();
        app.Install.FailChecks(new HttpRequestException("No network."));

        app.Start();
        await app.Install.WaitForCompletedChecksAsync(1);
        await UpdatesHarness.Pump();

        Assert.Equal(new UpdateState(UpdateStage.NoUpdate, "2.1.0", null, null), app.Updates.State);
    }

    [WpfFact]
    public async Task The_next_check_still_happens_after_a_failed_one()
    {
        var app = new UpdatesHarness();
        app.Install.FailChecks(new HttpRequestException("No network."));
        await app.StartAndFirstCheckDoneAsync();
        app.Install.SucceedChecks();
        app.Install.Found = UpdatesHarness.Newer("2.2.0");

        app.Time.Advance(FirstNight);

        var state = await app.WaitForStateAsync(s => s.Stage == UpdateStage.Available);
        Assert.Equal("2.2.0", state.NewVersion);
    }
}
