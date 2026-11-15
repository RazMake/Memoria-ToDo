using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>Stopping Updates: it cancels a check or a download under way, joins repeated stops, and starts the updater only after an update ended the app.</summary>
[Unit]
public class UpdateStopTests
{
    [WpfFact]
    public async Task Stop_cancels_a_check_under_way_and_ends()
    {
        var app = new UpdatesHarness();
        app.Install.HoldChecks();
        app.Start();
        await app.Install.WaitForChecksAsync(1);

        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        await app.Install.WaitForCheckCancellationAsync();
        Assert.Empty(app.Install.Applied);
    }

    [WpfFact]
    public async Task Stop_cancels_a_download_under_way_and_starts_no_updater()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);

        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        await app.Install.WaitForDownloadCancellationAsync();
        Assert.Equal(0, app.Exit.EndCount);
        Assert.Empty(app.Install.Applied);
    }

    [WpfFact]
    public async Task Stop_waits_for_the_download_to_end()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);

        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        Assert.Equal(1, app.Install.CompletedDownloadCount);
    }

    [WpfFact]
    public async Task Repeated_stops_join_the_same_stop_and_start_the_updater_once()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Updates.UpdateNow();
        await app.Exit.WaitForEndsAsync(1);

        var first = app.Updates.StopAsync();
        var second = app.Updates.StopAsync();
        await Task.WhenAll(first, second).WaitAsync(TestWait.ForSignal);
        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        Assert.Single(app.Install.Applied);
    }

    [WpfFact]
    public async Task Stop_without_an_update_starts_no_updater()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();

        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        Assert.Empty(app.Install.Applied);
    }

    [WpfFact]
    public async Task No_check_runs_after_stop()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();
        await app.Updates.StopAsync().WaitAsync(TestWait.ForSignal);

        app.Time.Advance(TimeSpan.FromDays(2));
        await UpdatesHarness.Pump();

        Assert.Equal(1, app.Install.CheckCount);
    }

    [WpfFact]
    public async Task A_updater_that_cannot_start_fails_the_stop()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.ApplyFailure = new InvalidOperationException("The updater is missing.");
        app.Updates.UpdateNow();
        await app.Exit.WaitForEndsAsync(1);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => app.Updates.StopAsync());

        Assert.Equal("The updater is missing.", failure.Message);
    }
}
