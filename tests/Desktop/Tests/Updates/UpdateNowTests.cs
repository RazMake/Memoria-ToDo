using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>An update you start: it downloads, asks Task View to close, ends the app, and starts the updater only when Updates stops.</summary>
[Unit]
public class UpdateNowTests
{
    [WpfFact]
    public async Task Turns_the_state_to_updating_the_new_version_while_it_downloads()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.HoldDownloads();

        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);

        Assert.Equal(new UpdateState(UpdateStage.Updating, "2.1.0", "2.2.0", null), app.Updates.State);
        Assert.Equal(0, app.Exit.CloseWindowsCount);
    }

    [WpfFact]
    public async Task Downloads_the_available_version_then_closes_the_windows_then_ends_the_app()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();

        app.Updates.UpdateNow();
        await app.Exit.WaitForEndsAsync(1);

        Assert.Equal(["2.2.0"], app.Install.Downloaded.Select(v => v.Version));
        Assert.Equal(["CloseWindows", "End"], app.Exit.Calls);
        Assert.Equal(UpdateStage.Updating, app.Updates.State.Stage);
    }

    [WpfFact]
    public async Task Does_not_close_the_windows_before_the_download_ends()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);
        await UpdatesHarness.Pump();

        Assert.Empty(app.Exit.Calls);

        app.Install.ReleaseDownloads();
        await app.Exit.WaitForEndsAsync(1);

        Assert.Equal(["CloseWindows", "End"], app.Exit.Calls);
    }

    [WpfFact]
    public async Task Starts_the_updater_only_when_Updates_stops_after_the_app_ended_and_not_quietly()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Updates.UpdateNow();
        await app.Exit.WaitForEndsAsync(1);
        await UpdatesHarness.Pump();
        Assert.Empty(app.Install.Applied);

        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        var applied = Assert.Single(app.Install.Applied);
        Assert.Equal("2.2.0", applied.Version.Version);
        Assert.False(applied.Quiet);
    }

    [WpfTheory]
    [InlineData("2.3.0")]
    [InlineData(null)]
    public async Task Installs_the_version_it_downloaded_when_a_check_that_was_already_running_ends_meanwhile(string? foundByCheck)
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        await app.Time.WaitForActiveTimerAsync();
        app.Install.HoldChecks();
        app.Time.Advance(TimeSpan.FromHours(3));
        await app.Install.WaitForChecksAsync(2);
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);
        app.Install.Found = foundByCheck is null ? null : UpdatesHarness.Newer(foundByCheck);

        app.Install.ReleaseChecks();
        await app.Install.WaitForCompletedChecksAsync(2);
        await UpdatesHarness.Pump();
        Assert.Equal(new UpdateState(UpdateStage.Updating, "2.1.0", "2.2.0", null), app.Updates.State);
        app.Install.ReleaseDownloads();
        await app.Exit.WaitForEndsAsync(1);
        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        Assert.Equal(["2.2.0"], app.Install.Downloaded.Select(v => v.Version));
        Assert.Equal("2.2.0", Assert.Single(app.Install.Applied).Version.Version);
    }

    [WpfFact]
    public async Task Does_nothing_when_no_version_is_available()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();

        app.Updates.UpdateNow();
        await UpdatesHarness.Pump();

        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Empty(app.Exit.Calls);
        Assert.Equal(UpdateStage.NoUpdate, app.Updates.State.Stage);
    }

    [WpfFact]
    public async Task Does_not_start_a_second_update_while_one_is_under_way()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);

        app.Updates.UpdateNow();
        await UpdatesHarness.Pump();

        Assert.Equal(1, app.Install.DownloadCount);
    }

    [WpfFact]
    public async Task Stays_available_and_ends_nothing_when_Task_View_stays_open()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Exit.CloseWindowsResult = false;

        app.Updates.UpdateNow();
        await app.Exit.WaitForCloseWindowsAsync(1);

        var state = await app.WaitForStateAsync(s => s.Stage == UpdateStage.Available);
        Assert.Equal(new UpdateState(UpdateStage.Available, "2.1.0", "2.2.0", null), state);
        Assert.Equal(0, app.Exit.EndCount);
        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);
        Assert.Empty(app.Install.Applied);
    }

    [WpfFact]
    public async Task Can_update_again_after_Task_View_stayed_open()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Exit.CloseWindowsResult = false;
        app.Updates.UpdateNow();
        await app.Exit.WaitForCloseWindowsAsync(1);
        await app.WaitForStateAsync(s => s.Stage == UpdateStage.Available);
        app.Exit.CloseWindowsResult = true;

        app.Updates.UpdateNow();

        await app.Exit.WaitForEndsAsync(1);
    }

    [WpfFact]
    public async Task A_failed_download_returns_to_available_with_the_failure_and_ends_nothing()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.FailDownloads(new HttpRequestException("No network."));

        app.Updates.UpdateNow();

        var state = await app.WaitForStateAsync(s => s.Failure is not null);
        Assert.Equal(new UpdateState(UpdateStage.Available, "2.1.0", "2.2.0", "The update to 2.2.0 failed: No network."), state);
        Assert.Empty(app.Exit.Calls);
    }

    [WpfFact]
    public async Task Can_update_again_after_a_failed_download()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.FailDownloads(new HttpRequestException("No network."));
        app.Updates.UpdateNow();
        await app.WaitForStateAsync(s => s.Failure is not null);
        app.Install.SucceedDownloads();

        app.Updates.UpdateNow();

        await app.Exit.WaitForEndsAsync(1);
    }
}
