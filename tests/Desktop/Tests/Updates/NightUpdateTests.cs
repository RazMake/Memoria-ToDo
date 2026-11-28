using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>
/// The night run with Auto update on: it updates by itself once no window is open, quietly. With Auto update off it only
/// checks. The check at start never updates by itself. The first night is 3 hours after the clock's start.
/// </summary>
[Unit]
public class NightUpdateTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan FirstNight = TimeSpan.FromHours(3);

    private static async Task<UpdatesHarness> StartedWithAutoUpdateAndVersionAsync(string autoUpdate = "on")
    {
        var app = new UpdatesHarness();
        app.WriteAutoUpdateFile(autoUpdate);
        await app.StartWithVersionAvailableAsync();
        await app.Time.WaitForActiveTimerAsync();
        return app;
    }

    [WpfFact]
    public async Task The_check_at_start_does_not_update_by_itself()
    {
        var app = await StartedWithAutoUpdateAndVersionAsync();
        await UpdatesHarness.Pump();

        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Equal(UpdateStage.Available, app.Updates.State.Stage);
    }

    [WpfFact]
    public async Task The_night_run_downloads_ends_the_app_and_starts_the_updater_quietly_on_stop()
    {
        var app = await StartedWithAutoUpdateAndVersionAsync();

        app.Time.Advance(FirstNight);
        await app.Exit.WaitForEndsAsync(1);
        await app.Updates.StopAsync().WaitAsync(Shared.Fakes.TestWait.ForSignal);

        Assert.Equal(["2.2.0"], app.Install.Downloaded.Select(v => v.Version));
        var applied = Assert.Single(app.Install.Applied);
        Assert.Equal("2.2.0", applied.Version.Version);
        Assert.True(applied.Quiet);
    }

    [WpfFact]
    public async Task The_night_run_waits_while_a_window_is_open_and_updates_within_an_hour_after_the_last_closes()
    {
        var app = await StartedWithAutoUpdateAndVersionAsync();
        app.Exit.AnyWindowOpen = true;
        app.Time.Advance(FirstNight);
        await app.Install.WaitForCompletedChecksAsync(2);
        app.Time.Advance(TimeSpan.FromHours(2));
        await UpdatesHarness.Pump();
        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Empty(app.Exit.Calls);
        Assert.Equal(UpdateStage.Available, app.Updates.State.Stage);

        app.Exit.AnyWindowOpen = false;
        app.Time.Advance(Hour);

        await app.Exit.WaitForEndsAsync(1);
        await app.Updates.StopAsync().WaitAsync(Shared.Fakes.TestWait.ForSignal);
        Assert.True(Assert.Single(app.Install.Applied).Quiet);
    }

    [WpfFact]
    public async Task With_Auto_update_off_the_night_run_only_checks()
    {
        var app = await StartedWithAutoUpdateAndVersionAsync("off");

        app.Time.Advance(FirstNight);
        await app.Install.WaitForCompletedChecksAsync(2);
        await UpdatesHarness.Pump();

        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Empty(app.Exit.Calls);
        Assert.Equal(UpdateStage.Available, app.Updates.State.Stage);
    }

    [WpfFact]
    public async Task A_failed_night_update_tries_again_the_next_night_and_not_before()
    {
        var app = await StartedWithAutoUpdateAndVersionAsync();
        app.Install.FailDownloads(new HttpRequestException("No network."));
        app.Time.Advance(FirstNight);
        await app.WaitForStateAsync(s => s.Failure is not null);
        await app.Time.WaitForActiveTimerAsync();
        app.Install.SucceedDownloads();

        app.Time.Advance(TimeSpan.FromHours(20));
        await UpdatesHarness.Pump();
        Assert.Equal(1, app.Install.DownloadCount);

        app.Time.Advance(TimeSpan.FromHours(4));

        await app.Exit.WaitForEndsAsync(1);
        Assert.Equal(2, app.Install.DownloadCount);
    }

    [WpfFact]
    public async Task Update_now_with_Auto_update_on_restarts_the_app_as_a_start_by_hand()
    {
        var app = await StartedWithAutoUpdateAndVersionAsync();

        app.Updates.UpdateNow();
        await app.Exit.WaitForEndsAsync(1);
        await app.Updates.StopAsync().WaitAsync(Shared.Fakes.TestWait.ForSignal);

        Assert.False(Assert.Single(app.Install.Applied).Quiet);
    }

    // The night run waits for a timer when it waits for windows. Fails with what the run did instead, rather than with a bare timeout.
    private static async Task AssertWaitingAsync(UpdatesHarness app)
    {
        try
        {
            await app.Time.WaitForActiveTimerAsync();
        }
        catch (TimeoutException)
        {
            Assert.Fail($"The night run didn't wait. Downloads started: {app.Install.DownloadCount}; calls to the app: [{string.Join(", ", app.Exit.Calls)}].");
        }
    }

    // A night run that waits for windows after the night's check, with a window open.
    private static async Task<UpdatesHarness> WaitingForWindowsAsync()
    {
        var app = await StartedWithAutoUpdateAndVersionAsync();
        app.Exit.AnyWindowOpen = true;
        app.Time.Advance(FirstNight);
        await app.Install.WaitForCompletedChecksAsync(2);
        await UpdatesHarness.Pump();
        await AssertWaitingAsync(app);
        return app;
    }

    [WpfFact]
    public async Task Turning_Auto_update_off_cancels_a_night_run_still_waiting_for_windows_to_close()
    {
        var app = await WaitingForWindowsAsync();

        app.Updates.SetAutoUpdate(false);
        app.Exit.AnyWindowOpen = false;
        app.Time.Advance(Hour);
        await AssertWaitingAsync(app);

        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Empty(app.Exit.Calls);
        Assert.Equal(UpdateStage.Available, app.Updates.State.Stage);
    }

    [WpfFact]
    public async Task Turning_Auto_update_off_and_on_again_while_a_night_run_waits_leaves_the_update_to_the_next_night()
    {
        var app = await WaitingForWindowsAsync();

        app.Updates.SetAutoUpdate(false);
        app.Updates.SetAutoUpdate(true);
        app.Exit.AnyWindowOpen = false;
        await app.AdvanceHourlyAsync(10);
        await UpdatesHarness.Pump();

        Assert.Equal(0, app.Install.DownloadCount);
        Assert.Empty(app.Exit.Calls);
    }

    [WpfFact]
    public async Task A_night_run_waiting_for_windows_does_not_stop_the_nightly_checks()
    {
        var app = await WaitingForWindowsAsync();

        for (var night = 3; night <= 4; night++)
        {
            await app.AdvanceHourlyAsync(24);
            try
            {
                await app.Install.WaitForChecksAsync(night);
            }
            catch (TimeoutException)
            {
                Assert.Fail($"Expected {night} checks, one each night, but only {app.Install.CheckCount} happened.");
            }

            await app.Install.WaitForCompletedChecksAsync(night);
            await UpdatesHarness.Pump();
        }

        Assert.Equal(0, app.Install.DownloadCount);
    }

    [WpfFact]
    public async Task A_newer_version_found_while_a_night_run_waits_replaces_the_one_it_waits_to_install()
    {
        var app = await WaitingForWindowsAsync();
        app.Install.Found = UpdatesHarness.Newer("2.3.0");
        await app.AdvanceHourlyAsync(24);
        await app.WaitForStateAsync(s => s.NewVersion == "2.3.0");
        await UpdatesHarness.Pump();
        await AssertWaitingAsync(app);

        app.Exit.AnyWindowOpen = false;
        await app.AdvanceHourlyAsync(1);
        await app.Exit.WaitForEndsAsync(1);
        await app.Updates.StopAsync().WaitAsync(Shared.Fakes.TestWait.ForSignal);

        Assert.Equal(["2.3.0"], app.Install.Downloaded.Select(v => v.Version));
        var applied = Assert.Single(app.Install.Applied);
        Assert.Equal("2.3.0", applied.Version.Version);
        Assert.True(applied.Quiet);
    }

    [WpfFact]
    public async Task A_window_opened_during_the_night_run_download_holds_the_restart_until_every_window_is_closed_again()
    {
        var app = await StartedWithAutoUpdateAndVersionAsync();
        app.Install.HoldDownloads();
        app.Time.Advance(FirstNight);
        await app.Install.WaitForDownloadsAsync(1);
        app.Exit.AnyWindowOpen = true;

        app.Install.ReleaseDownloads();
        await app.Install.WaitForCompletedDownloadsAsync(1);
        await UpdatesHarness.Pump();
        await AssertWaitingAsync(app);

        Assert.Empty(app.Exit.Calls);
        Assert.Equal(UpdateStage.Updating, app.Updates.State.Stage);

        app.Exit.AnyWindowOpen = false;
        app.Time.Advance(Hour);

        await app.Exit.WaitForEndsAsync(1);
        await app.Updates.StopAsync().WaitAsync(Shared.Fakes.TestWait.ForSignal);
        Assert.True(Assert.Single(app.Install.Applied).Quiet);
    }
}
