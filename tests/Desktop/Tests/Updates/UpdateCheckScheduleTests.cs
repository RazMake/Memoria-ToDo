using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>
/// When checks run: once at start, then each night at 3:00 local time, within an hour of waking when the PC slept through
/// it, and never while an update is under way. The clock starts at 2026-01-01 00:00 UTC, in a UTC local time zone unless a test says.
/// </summary>
[Unit]
public class UpdateCheckScheduleTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    // The Windows ids, as the tests run on Windows: US Pacific time, 8 hours behind UTC, then 7 hours in daylight saving time.
    private static TimeZoneInfo Pacific => TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    // Fails with the number of checks seen, instead of a bare timeout, when the check never comes.
    private static async Task AssertCheckedAsync(UpdatesHarness app, int count)
    {
        try
        {
            await app.Install.WaitForChecksAsync(count);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Expected {count} update checks, but only {app.Install.CheckCount} happened.");
        }
    }

    private static async Task AssertNoCheckBeyondAsync(UpdatesHarness app, int count)
    {
        await UpdatesHarness.Pump();
        Assert.Equal(count, app.Install.CheckCount);
    }

    [WpfFact]
    public async Task Start_checks_once_without_waiting_for_the_check()
    {
        var app = new UpdatesHarness();
        app.Install.HoldChecks();

        app.Start();
        await app.Install.WaitForChecksAsync(1);

        Assert.Equal(1, app.Install.CheckCount);
        Assert.Equal(0, app.Install.CompletedCheckCount);
        Assert.Equal(UpdateStage.NoUpdate, app.Updates.State.Stage);
    }

    [WpfFact]
    public async Task Does_not_check_again_before_3_in_the_morning()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Advance(TimeSpan.FromHours(3) - TimeSpan.FromSeconds(1));

        await AssertNoCheckBeyondAsync(app, 1);
    }

    [WpfFact]
    public async Task Checks_at_3_in_the_morning()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Advance(TimeSpan.FromHours(3));

        await AssertCheckedAsync(app, 2);
    }

    [WpfFact]
    public async Task Checks_once_each_night_and_not_again_before_the_next()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();

        for (var night = 2; night <= 4; night++)
        {
            app.Time.Advance(night == 2 ? TimeSpan.FromHours(3) : TimeSpan.FromHours(24) - Hour);
            if (night > 2)
            {
                await AssertNoCheckBeyondAsync(app, night - 1);
                app.Time.Advance(Hour);
            }

            await AssertCheckedAsync(app, night);
            await app.Install.WaitForCompletedChecksAsync(night);
            await UpdatesHarness.Pump();
            await app.Time.WaitForActiveTimerAsync();
        }

        Assert.Equal(4, app.Install.CheckCount);
    }

    [WpfFact]
    public async Task Checks_at_3_local_time_not_3_UTC()
    {
        var app = new UpdatesHarness(zone: TimeZoneInfo.CreateCustomTimeZone("Test minus 8", TimeSpan.FromHours(-8), "Test minus 8", "Test minus 8"));
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Advance(TimeSpan.FromHours(11) - TimeSpan.FromSeconds(1));
        await AssertNoCheckBeyondAsync(app, 1);

        app.Time.Advance(TimeSpan.FromSeconds(1));
        await AssertCheckedAsync(app, 2);
    }

    [WpfFact]
    public async Task A_check_the_PC_slept_through_happens_within_an_hour_of_waking()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Sleep(TimeSpan.FromHours(10));
        app.Time.Advance(Hour);

        await AssertCheckedAsync(app, 2);
    }

    [WpfFact]
    public async Task A_check_after_a_sleep_of_more_than_a_day_happens_once_within_an_hour_of_waking()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Sleep(TimeSpan.FromHours(50));
        app.Time.Advance(Hour);
        await AssertCheckedAsync(app, 2);
        await app.Install.WaitForCompletedChecksAsync(2);
        await UpdatesHarness.Pump();
        await app.Time.WaitForActiveTimerAsync();
        app.Time.Advance(TimeSpan.FromHours(2));

        await AssertNoCheckBeyondAsync(app, 2);
    }

    [WpfFact]
    public async Task A_sleep_that_ends_before_3_does_not_bring_the_check_forward()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Sleep(Hour);
        app.Time.Advance(Hour);

        await AssertNoCheckBeyondAsync(app, 1);
    }

    [WpfFact]
    public async Task Stays_at_3_local_time_when_daylight_saving_time_starts()
    {
        // 3:00 on March 8, 2026 in Pacific time is 10:00 UTC, 22 hours after the start; 24 hours after March 7's 3:00 would be 11:00 UTC.
        var app = new UpdatesHarness(new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.Zero), Pacific);
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Advance(TimeSpan.FromHours(22) - TimeSpan.FromSeconds(1));
        await AssertNoCheckBeyondAsync(app, 1);

        app.Time.Advance(TimeSpan.FromSeconds(1));
        await AssertCheckedAsync(app, 2);
    }

    [WpfFact]
    public async Task Stays_at_3_local_time_when_daylight_saving_time_ends()
    {
        // 3:00 on November 1, 2026 in Pacific time is 11:00 UTC, 23 hours after the start; the clocks went back at 2:00.
        var app = new UpdatesHarness(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero), Pacific);
        await app.StartAndFirstCheckDoneAsync();

        app.Time.Advance(TimeSpan.FromHours(23) - TimeSpan.FromSeconds(1));
        await AssertNoCheckBeyondAsync(app, 1);

        app.Time.Advance(TimeSpan.FromSeconds(1));
        await AssertCheckedAsync(app, 2);
    }

    [WpfFact]
    public async Task Changing_the_time_zone_while_waiting_moves_the_next_check_to_3_in_the_new_zone_within_an_hour()
    {
        var app = new UpdatesHarness();
        await app.StartAndFirstCheckDoneAsync();
        app.Time.UseLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test minus 8", TimeSpan.FromHours(-8), "Test minus 8", "Test minus 8"));

        await app.AdvanceHourlyAsync(10); // Past 3:00 UTC, the old night; the new one is at 11:00 UTC.
        await AssertNoCheckBeyondAsync(app, 1);

        await app.AdvanceHourlyAsync(2);
        await AssertCheckedAsync(app, 2);
    }

    [WpfFact]
    public async Task Moving_to_a_zone_where_the_next_3_comes_sooner_than_the_planned_night_checks_at_that_3()
    {
        // At the start it is 08:00 at UTC+8, so the planned night is January 2 at 03:00 there. In UTC, 03:00 comes in 3 hours.
        var app = new UpdatesHarness(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.CreateCustomTimeZone("Test plus 8", TimeSpan.FromHours(8), "Test plus 8", "Test plus 8"));
        await app.StartAndFirstCheckDoneAsync();
        app.Time.UseLocalTimeZone(TimeZoneInfo.Utc);

        await app.AdvanceHourlyAsync(2);
        await AssertNoCheckBeyondAsync(app, 1);

        await app.AdvanceHourlyAsync(2);
        await AssertCheckedAsync(app, 2);
    }

    [WpfFact]
    public async Task No_check_runs_while_an_update_is_under_way()
    {
        var app = new UpdatesHarness();
        await app.StartWithVersionAvailableAsync();
        app.Install.HoldDownloads();
        app.Updates.UpdateNow();
        await app.Install.WaitForDownloadsAsync(1);

        app.Time.Advance(TimeSpan.FromDays(2));

        await AssertNoCheckBeyondAsync(app, 1);
    }
}
