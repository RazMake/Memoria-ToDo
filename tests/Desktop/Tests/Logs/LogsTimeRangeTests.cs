using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>The time range: what each preset fills into the From and To boxes, and what the boxes themselves filter. The clock reads 2026-03-14 14:30:45 local time.</summary>
[Unit]
public class LogsTimeRangeTests
{
    private readonly LogsServices _services = new();

    private CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string[] Messages(LogsModel model) => [.. model.Rows.Select(row => row.Message)];

    private Task<LogsModel> LoadedWithHoursAsync() => _services
        .With(
            LogsServices.Entry("2026-03-14 14:10", message: "Now-ish"),
            LogsServices.Entry("2026-03-14 13:29:59.999", message: "Just over an hour"),
            LogsServices.Entry("2026-03-14 00:00", message: "Midnight"),
            LogsServices.Entry("2026-03-13 23:59:59.999", message: "Before midnight"),
            LogsServices.Entry("2026-03-13 14:30", message: "A day ago"),
            LogsServices.Entry("2026-03-10 12:00", message: "Four days ago"),
            LogsServices.Entry("2026-03-01 12:00", message: "Two weeks ago"))
        .LoadedAsync(Cancel);

    [Fact]
    public async Task Starts_on_All_with_both_boxes_empty_and_shows_every_entry()
    {
        var model = await LoadedWithHoursAsync();

        Assert.Equal(TimePreset.All, model.Preset);
        Assert.Equal("", model.From);
        Assert.Equal("", model.To);
        Assert.Equal(7, model.Rows.Count);
    }

    [Theory]
    [InlineData(nameof(TimePreset.LastHour), "2026-03-14 13:30")]
    [InlineData(nameof(TimePreset.Last24Hours), "2026-03-13 14:30")]
    [InlineData(nameof(TimePreset.Last7Days), "2026-03-07 14:30")]
    [InlineData(nameof(TimePreset.Today), "2026-03-14 00:00")]
    public async Task A_preset_fills_From_with_its_start_in_local_time_and_empties_To(string presetName, string from)
    {
        var preset = Enum.Parse<TimePreset>(presetName);
        var model = await LoadedWithHoursAsync();
        model.To = "2026-03-14 12:00";

        model.Preset = preset;

        Assert.Equal(preset, model.Preset);
        Assert.Equal(from, model.From);
        Assert.Equal("", model.To);
    }

    [Fact]
    public async Task All_empties_both_boxes_again_after_another_preset()
    {
        var model = await LoadedWithHoursAsync();
        model.Preset = TimePreset.LastHour;

        model.Preset = TimePreset.All;

        Assert.Equal("", model.From);
        Assert.Equal("", model.To);
        Assert.Equal(7, model.Rows.Count);
    }

    [Fact]
    public async Task Last_hour_shows_the_entries_from_the_minute_an_hour_ago_on()
    {
        var model = await LoadedWithHoursAsync();

        model.Preset = TimePreset.LastHour;

        Assert.Equal(["Now-ish"], Messages(model));
    }

    [Fact]
    public async Task Today_shows_the_entries_from_local_midnight_on_including_midnight_itself()
    {
        var model = await LoadedWithHoursAsync();

        model.Preset = TimePreset.Today;

        Assert.Equal(["Now-ish", "Just over an hour", "Midnight"], Messages(model));
    }

    [Fact]
    public async Task Last_24_hours_and_Last_7_days_show_the_entries_since_their_start()
    {
        var model = await LoadedWithHoursAsync();

        model.Preset = TimePreset.Last24Hours;
        var day = Messages(model);
        model.Preset = TimePreset.Last7Days;
        var week = Messages(model);

        Assert.Equal(["Now-ish", "Just over an hour", "Midnight", "Before midnight", "A day ago"], day);
        Assert.Equal([.. day, "Four days ago"], week);
    }

    [Fact]
    public async Task Editing_a_box_changes_the_preset_to_Custom()
    {
        var model = await LoadedWithHoursAsync();
        model.Preset = TimePreset.LastHour;

        model.From = "2026-03-14 13:00";

        Assert.Equal(TimePreset.Custom, model.Preset);
        Assert.Equal("2026-03-14 13:00", model.From);
    }

    [Fact]
    public async Task Choosing_Custom_fills_an_empty_To_with_the_current_minute_and_keeps_From()
    {
        var model = await LoadedWithHoursAsync();
        model.Preset = TimePreset.Today;

        model.Preset = TimePreset.Custom;

        Assert.Equal("2026-03-14 14:30", model.To);
        Assert.Equal("2026-03-14 00:00", model.From);
        Assert.Equal(TimePreset.Custom, model.Preset);
    }

    [Fact]
    public async Task Choosing_Custom_keeps_a_To_that_is_already_there()
    {
        var model = await LoadedWithHoursAsync();
        model.To = "2026-03-14 12:00";

        model.Preset = TimePreset.Custom;

        Assert.Equal("2026-03-14 12:00", model.To);
    }

    [Fact]
    public async Task Editing_To_also_changes_the_preset_to_Custom_and_keeps_From()
    {
        var model = await LoadedWithHoursAsync();
        model.Preset = TimePreset.Today;

        model.To = "2026-03-14 12:00";

        Assert.Equal(TimePreset.Custom, model.Preset);
        Assert.Equal("2026-03-14 00:00", model.From);
    }

    [Fact]
    public async Task Each_bound_includes_its_own_minute()
    {
        var model = await _services
            .With(
                LogsServices.Entry("2026-03-14 10:39:59.999", message: "Just before"),
                LogsServices.Entry("2026-03-14 10:40:00.000", message: "First second"),
                LogsServices.Entry("2026-03-14 10:45:59.999", message: "Last moment"),
                LogsServices.Entry("2026-03-14 10:46:00.000", message: "Just after"))
            .LoadedAsync(Cancel);

        model.From = "2026-03-14 10:40";
        model.To = "2026-03-14 10:45";

        Assert.Equal(["Last moment", "First second"], Messages(model));
    }

    [Fact]
    public async Task An_empty_box_sets_no_bound()
    {
        var model = await LoadedWithHoursAsync();

        model.From = "";
        model.To = "2026-03-13 23:59";

        Assert.Equal(["Before midnight", "A day ago", "Four days ago", "Two weeks ago"], Messages(model));

        model.From = "2026-03-13 23:59";
        model.To = "";

        Assert.Equal(["Now-ish", "Just over an hour", "Midnight", "Before midnight"], Messages(model));
    }

    [Theory]
    [InlineData("tomorrow")]
    [InlineData("2026-03-14")]
    [InlineData("2026-13-45 10:00")]
    public async Task A_value_that_is_not_a_date_and_time_shows_red_and_the_time_range_stops_filtering(string text)
    {
        var model = await LoadedWithHoursAsync();
        model.To = "2026-03-14 00:00";
        Assert.Equal(["Midnight", "Before midnight", "A day ago", "Four days ago", "Two weeks ago"], Messages(model));

        model.From = text;

        Assert.True(model.FromIsInvalid);
        Assert.False(model.ToIsInvalid);
        Assert.Equal(7, model.Rows.Count);
    }

    [Fact]
    public async Task A_To_of_the_last_minute_the_calendar_has_means_no_upper_limit_and_is_not_red()
    {
        var model = await LoadedWithHoursAsync();

        model.To = "9999-12-31 23:59";

        Assert.False(model.ToIsInvalid);
        Assert.False(model.FromIsInvalid);
        Assert.Equal(7, model.Rows.Count);
    }

    [Fact]
    public async Task A_From_and_To_both_at_the_last_minute_the_calendar_has_show_no_entries_of_today_and_are_not_red()
    {
        var model = await LoadedWithHoursAsync();

        model.From = "9999-12-31 23:59";
        model.To = "9999-12-31 23:59";

        Assert.False(model.FromIsInvalid);
        Assert.False(model.ToIsInvalid);
        Assert.DoesNotContain("Now-ish", Messages(model));
    }

    [Fact]
    public async Task A_valid_value_is_not_red()
    {
        var model = await LoadedWithHoursAsync();

        model.From = "2026-03-14 00:00";
        model.To = "2026-03-14 23:59";

        Assert.False(model.FromIsInvalid);
        Assert.False(model.ToIsInvalid);
    }

    [Fact]
    public async Task A_From_later_than_To_shows_red_and_the_time_range_stops_filtering()
    {
        var model = await LoadedWithHoursAsync();

        model.From = "2026-03-14 12:00";
        model.To = "2026-03-14 11:00";

        Assert.True(model.FromIsInvalid);
        Assert.True(model.ToIsInvalid);
        Assert.Equal(7, model.Rows.Count);
    }

    [Fact]
    public async Task Fixing_a_red_box_filters_again()
    {
        var model = await LoadedWithHoursAsync();
        model.From = "tomorrow";

        model.From = "2026-03-14 00:00";

        Assert.False(model.FromIsInvalid);
        Assert.Equal(["Now-ish", "Just over an hour", "Midnight"], Messages(model));
    }

    [Fact]
    public async Task Picking_a_preset_clears_a_red_box()
    {
        var model = await LoadedWithHoursAsync();
        model.From = "tomorrow";

        model.Preset = TimePreset.Today;

        Assert.False(model.FromIsInvalid);
        Assert.Equal("2026-03-14 00:00", model.From);
    }
}
