using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>The time range on the day the clock skips an hour in spring: the zone is UTC+1 that skips 02:00 to 03:00 on 2026-03-29.</summary>
[Unit]
public class LogsSpringClockChangeTests
{
    private readonly LogsServices _services = new();

    private CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static TimeZoneInfo ZoneThatSkipsAnHour()
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday));
        return TimeZoneInfo.CreateCustomTimeZone("Skipping zone", TimeSpan.FromHours(1), "Skipping zone", "Standard", "Summer", [rule]);
    }

    [Fact]
    public async Task A_From_and_To_that_cross_as_moments_in_the_skipped_hour_show_both_red_and_stop_filtering()
    {
        _services.Time.UseLocalTimeZone(ZoneThatSkipsAnHour());
        var model = await _services
            .With(
                new LogEntry(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), Serilog.Events.LogEventLevel.Information, "Sync", "Before", null),
                new LogEntry(new DateTimeOffset(2026, 3, 29, 2, 30, 0, TimeSpan.Zero), Serilog.Events.LogEventLevel.Information, "Sync", "After", null))
            .LoadedAsync(Cancel);

        // 02:30 doesn't exist that day, so as a moment it is 03:30 local, later than the end of 03:10.
        model.From = "2026-03-29 02:30";
        model.To = "2026-03-29 03:10";

        Assert.True(model.FromIsInvalid);
        Assert.True(model.ToIsInvalid);
        Assert.Equal(2, model.Rows.Count);
    }

    [Fact]
    public async Task A_range_inside_the_skipped_hour_shows_both_red_and_stops_filtering_without_throwing()
    {
        _services.Time.UseLocalTimeZone(ZoneThatSkipsAnHour());
        var model = await _services
            .With(
                new LogEntry(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), Serilog.Events.LogEventLevel.Information, "Sync", "Before", null),
                new LogEntry(new DateTimeOffset(2026, 3, 29, 2, 30, 0, TimeSpan.Zero), Serilog.Events.LogEventLevel.Information, "Sync", "After", null))
            .LoadedAsync(Cancel);

        // 02:30 and 02:59 don't exist that day: as moments they are 03:30 and 03:59 local, but the minute after 02:59 is 03:00 local, an earlier moment.
        model.From = "2026-03-29 02:30";
        model.To = "2026-03-29 02:59";

        Assert.True(model.FromIsInvalid);
        Assert.True(model.ToIsInvalid);
        Assert.Equal(2, model.Rows.Count);
    }

    [Fact]
    public async Task A_range_that_ends_inside_the_skipped_hour_keeps_the_entries_before_the_skip_and_drops_those_after()
    {
        _services.Time.UseLocalTimeZone(ZoneThatSkipsAnHour());
        var model = await _services
            .With(
                new LogEntry(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), Serilog.Events.LogEventLevel.Information, "Sync", "Before", null),
                new LogEntry(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), Serilog.Events.LogEventLevel.Information, "Sync", "After", null))
            .LoadedAsync(Cancel);

        // 00:30Z is 01:30 local, before the skip; 01:30Z is 03:30 local, after it.
        model.From = "2026-03-29 01:00";
        model.To = "2026-03-29 02:59";

        Assert.False(model.FromIsInvalid);
        Assert.False(model.ToIsInvalid);
        Assert.Equal(["Before"], model.Rows.Select(row => row.Message));
    }
}
