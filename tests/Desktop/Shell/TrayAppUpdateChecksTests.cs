using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppUpdateChecksTests
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    private static async Task<ShellHarness> StartedAndFirstCheckDone()
    {
        var app = new ShellHarness();
        app.Start();
        await app.Updates.WaitForCompletedCallsAsync(1);
        await app.Time.WaitForActiveTimerAsync();
        return app;
    }

    [Fact]
    public async Task Start_checks_for_updates_once()
    {
        var app = new ShellHarness();

        app.Start();
        await app.Updates.WaitForCallsAsync(1);

        Assert.Equal(1, app.Updates.CallCount);
    }

    [Fact]
    public async Task Start_does_not_wait_for_the_update_check()
    {
        var app = new ShellHarness();
        app.Updates.Hold();

        app.Start();
        await app.Updates.WaitForCallsAsync(1);

        Assert.True(app.TrayIcon.IsShown);
        Assert.Equal(0, app.Updates.CompletedCallCount);
    }

    [Fact]
    public async Task Does_not_check_again_before_24_hours_have_passed()
    {
        var app = await StartedAndFirstCheckDone();

        app.Time.Advance(Day - TimeSpan.FromSeconds(1));

        Assert.Equal(1, app.Updates.CallCount);
    }

    [Fact]
    public async Task Checks_again_24_hours_after_the_start()
    {
        var app = await StartedAndFirstCheckDone();

        app.Time.Advance(Day);
        await app.Updates.WaitForCallsAsync(2);

        Assert.Equal(2, app.Updates.CallCount);
    }

    [Fact]
    public async Task Checks_once_a_day_for_as_long_as_the_app_runs()
    {
        var app = await StartedAndFirstCheckDone();

        for (var check = 2; check <= 4; check++)
        {
            app.Time.Advance(Day);
            await app.Updates.WaitForCompletedCallsAsync(check);
            await app.Time.WaitForActiveTimerAsync();
        }

        Assert.Equal(4, app.Updates.CallCount);
    }

    [Fact]
    public async Task Counts_the_24_hours_from_when_the_previous_check_began()
    {
        var app = new ShellHarness();
        app.Updates.Hold();
        app.Start();
        await app.Updates.WaitForCallsAsync(1);
        app.Time.Advance(TimeSpan.FromHours(1));
        app.Updates.Release();
        await app.Updates.WaitForCompletedCallsAsync(1);
        await app.Time.WaitForActiveTimerAsync();

        app.Time.Advance(Day - TimeSpan.FromHours(1) - TimeSpan.FromSeconds(1));
        Assert.Equal(1, app.Updates.CallCount);

        app.Time.Advance(TimeSpan.FromSeconds(1));
        await app.Updates.WaitForCallsAsync(2);

        Assert.Equal(2, app.Updates.CallCount);
    }

    [Fact]
    public async Task A_failed_check_shows_nothing_and_leaves_the_app_running()
    {
        var app = new ShellHarness();
        app.Updates.Fail(new HttpRequestException("No network."));

        app.Start();
        await app.Updates.WaitForCompletedCallsAsync(1);

        Assert.True(app.TrayIcon.IsShown);
        Assert.Equal(0, app.Add.ShowCount);
        Assert.Equal(0, app.ViewTasks.ShowCount);
        Assert.Equal(0, app.Configure.ShowCount);
        Assert.Equal(0, app.EndAppCalls);
    }

    [Fact]
    public async Task The_next_check_still_happens_after_a_failed_one()
    {
        var app = new ShellHarness();
        app.Updates.Fail(new HttpRequestException("No network."));
        app.Start();
        await app.Updates.WaitForCompletedCallsAsync(1);
        await app.Time.WaitForActiveTimerAsync();
        app.Updates.Succeed();

        app.Time.Advance(Day);
        await app.Updates.WaitForCompletedCallsAsync(2);

        Assert.Equal(2, app.Updates.CallCount);
        Assert.True(app.TrayIcon.IsShown);
    }

    [Fact]
    public async Task Stop_cancels_a_check_that_is_running()
    {
        var app = new ShellHarness();
        app.Updates.Hold();
        app.Start();
        await app.Updates.WaitForCallsAsync(1);

        app.Lifetime.Stop();
        await app.Updates.WaitForCancellationAsync();

        Assert.Equal(1, app.Updates.CompletedCallCount);
    }

    [Fact]
    public async Task Stop_ends_the_checks()
    {
        var app = await StartedAndFirstCheckDone();

        app.Lifetime.Stop();
        app.Time.Advance(Day * 3);

        Assert.Equal(1, app.Updates.CallCount);
    }

    [Fact]
    public async Task Checks_within_an_hour_of_awake_time_after_the_check_fell_due_during_sleep()
    {
        var app = await StartedAndFirstCheckDone();

        app.Time.Sleep(TimeSpan.FromHours(23));
        app.Time.Advance(TimeSpan.FromHours(1));

        await AssertChecksHappenedAsync(app, 2);
    }

    [Fact]
    public async Task Checks_within_an_hour_of_waking_from_a_sleep_longer_than_a_day()
    {
        var app = await StartedAndFirstCheckDone();

        app.Time.Sleep(TimeSpan.FromHours(30));
        app.Time.Advance(TimeSpan.FromHours(1));

        await AssertChecksHappenedAsync(app, 2);
    }

    [Fact]
    public async Task Sleeping_does_not_bring_the_next_check_forward()
    {
        var app = await StartedAndFirstCheckDone();

        app.Time.Sleep(TimeSpan.FromHours(10));
        app.Time.Advance(TimeSpan.FromHours(13));

        Assert.Equal(1, app.Updates.CallCount);
    }

    [Fact]
    public void Tests_run_without_a_synchronization_context_so_checks_begin_inside_Advance()
    {
        // The tests that assert right after Advance that no check began rely on this: with a context, a check that
        // began too early could still be queued when they look.
        Assert.Null(SynchronizationContext.Current);
    }

    // Fails with the number of checks seen, instead of a bare timeout, when the check never comes.
    private static async Task AssertChecksHappenedAsync(ShellHarness app, int count)
    {
        try
        {
            await app.Updates.WaitForCallsAsync(count);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Expected {count} update checks, but only {app.Updates.CallCount} happened.");
        }
    }
}
