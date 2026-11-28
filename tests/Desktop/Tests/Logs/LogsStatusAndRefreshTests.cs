using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>The status line under the list, and Refresh (also F5), which reads the source again and keeps the filters.</summary>
[Unit]
public class LogsStatusAndRefreshTests
{
    private readonly LogsServices _services = new();

    private CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string[] Messages(LogsModel model) => [.. model.Rows.Select(row => row.Message)];

    private static LogEntry Entry(string local, string message, LogEventLevel level = LogEventLevel.Information, string component = "Sync") =>
        LogsServices.Entry(local, level, component, message);

    // Status line

    [Fact]
    public async Task Says_how_many_of_all_the_loaded_entries_show()
    {
        var model = await _services
            .With(
                Entry("2026-03-14 10:01", "one"),
                Entry("2026-03-14 10:02", "two"),
                Entry("2026-03-14 10:03", "three", LogEventLevel.Warning),
                Entry("2026-03-14 10:04", "four", LogEventLevel.Debug),
                Entry("2026-03-14 10:05", "five", LogEventLevel.Warning))
            .LoadedAsync(Cancel);

        Assert.Contains("Showing 4 of 5 entries", model.StatusText);
    }

    [Fact]
    public async Task Follows_every_filter_change()
    {
        var model = await _services
            .With(
                Entry("2026-03-14 10:01", "one"),
                Entry("2026-03-14 10:02", "two", LogEventLevel.Warning),
                Entry("2026-03-14 10:03", "three", LogEventLevel.Warning, "Tasks"))
            .LoadedAsync(Cancel);

        model.MinimumLevel = LogEventLevel.Warning;
        Assert.Contains("Showing 2 of 3 entries", model.StatusText);

        model.Components.Single(choice => choice.Name == "Tasks").IsChecked = false;
        Assert.Contains("Showing 1 of 3 entries", model.StatusText);

        model.SearchText = "absent";
        Assert.Contains("Showing 0 of 3 entries", model.StatusText);

        model.SearchText = "";
        model.AllComponentsChecked = true;
        model.MinimumLevel = LogEventLevel.Information;
        Assert.Contains("Showing 3 of 3 entries", model.StatusText);
    }

    [Fact]
    public async Task Says_how_many_lines_could_not_be_read_when_some_are_broken()
    {
        _services.Source.UnreadableLines = 3;

        var model = await _services.With(Entry("2026-03-14 10:01", "one")).LoadedAsync(Cancel);

        Assert.Contains("3 lines couldn't be read", model.StatusText);
    }

    [Fact]
    public async Task Says_nothing_about_unreadable_lines_when_none_are_broken()
    {
        var model = await _services.With(Entry("2026-03-14 10:01", "one")).LoadedAsync(Cancel);

        Assert.DoesNotContain("couldn't be read", model.StatusText);
    }

    [Fact]
    public async Task Says_why_the_log_cannot_be_written_while_the_writer_is_failing()
    {
        _services.Source.WriteProblem = "The disk is full";

        var model = await _services.With(Entry("2026-03-14 10:01", "one")).LoadedAsync(Cancel);

        Assert.Contains("The log can't be written: The disk is full", model.StatusText);
    }

    [Fact]
    public async Task Says_nothing_about_writing_while_the_writer_works()
    {
        var model = await _services.With(Entry("2026-03-14 10:01", "one")).LoadedAsync(Cancel);

        Assert.DoesNotContain("can't be written", model.StatusText);
    }

    [Fact]
    public async Task Says_all_three_things_when_all_three_apply()
    {
        _services.Source.UnreadableLines = 2;
        _services.Source.WriteProblem = "Access denied";

        var model = await _services.With(Entry("2026-03-14 10:01", "one")).LoadedAsync(Cancel);

        Assert.Contains("Showing 1 of 1 entries", model.StatusText);
        Assert.Contains("2 lines couldn't be read", model.StatusText);
        Assert.Contains("The log can't be written: Access denied", model.StatusText);
    }

    // Refresh

    [Fact]
    public async Task Loading_again_reads_the_source_again_and_shows_the_new_entries()
    {
        var model = await _services.With(Entry("2026-03-14 10:01", "old")).LoadedAsync(Cancel);
        _services.Source.Entries.Add(Entry("2026-03-14 10:02", "new"));

        await model.LoadAsync(Cancel);

        Assert.Equal(2, _services.Source.ReadCount);
        Assert.Equal(["new", "old"], Messages(model));
    }

    [Fact]
    public async Task Loading_again_keeps_the_level_the_search_and_the_unchecked_components()
    {
        var model = await _services
            .With(
                Entry("2026-03-14 10:01", "keep this one", LogEventLevel.Warning, "Sync"),
                Entry("2026-03-14 10:02", "keep this too", LogEventLevel.Information, "Sync"),
                Entry("2026-03-14 10:03", "keep that", LogEventLevel.Warning, "Tasks"))
            .LoadedAsync(Cancel);
        model.MinimumLevel = LogEventLevel.Warning;
        model.SearchText = "KEEP";
        model.Components.Single(choice => choice.Name == "Tasks").IsChecked = false;
        _services.Source.Entries.Add(Entry("2026-03-14 10:04", "keep newer", LogEventLevel.Error, "Sync"));
        _services.Source.Entries.Add(Entry("2026-03-14 10:05", "keep other", LogEventLevel.Warning, "Tasks"));

        await model.LoadAsync(Cancel);

        Assert.Equal(LogEventLevel.Warning, model.MinimumLevel);
        Assert.Equal("KEEP", model.SearchText);
        Assert.Equal(["keep newer", "keep this one"], Messages(model));
    }

    [Fact]
    public async Task Loading_again_checks_a_component_that_is_new()
    {
        var model = await _services.With(Entry("2026-03-14 10:01", "old", component: "Sync")).LoadedAsync(Cancel);
        _services.Source.Entries.Add(Entry("2026-03-14 10:02", "from a new component", component: "Tasks"));

        await model.LoadAsync(Cancel);

        Assert.Equal(["Sync", "Tasks"], model.Components.Select(choice => choice.Name));
        Assert.All(model.Components, choice => Assert.True(choice.IsChecked));
    }

    [Fact]
    public async Task Loading_again_works_the_selected_preset_out_again_from_the_time_now()
    {
        var model = await _services
            .With(Entry("2026-03-14 14:10", "recent"), Entry("2026-03-14 13:50", "soon too old"))
            .LoadedAsync(Cancel);
        model.Preset = TimePreset.LastHour;
        Assert.Equal("2026-03-14 13:30", model.From);
        Assert.Equal(["recent", "soon too old"], Messages(model));

        _services.Time.Advance(TimeSpan.FromMinutes(30));
        await model.LoadAsync(Cancel);

        Assert.Equal(TimePreset.LastHour, model.Preset);
        Assert.Equal("2026-03-14 14:00", model.From);
        Assert.Equal(["recent"], Messages(model));
    }

    [Fact]
    public async Task Loading_again_keeps_the_boxes_the_user_typed()
    {
        var model = await _services
            .With(Entry("2026-03-14 10:41", "inside"), Entry("2026-03-14 10:50", "outside"))
            .LoadedAsync(Cancel);
        model.From = "2026-03-14 10:40";
        model.To = "2026-03-14 10:45";
        _services.Time.Advance(TimeSpan.FromHours(1));

        await model.LoadAsync(Cancel);

        Assert.Equal(TimePreset.Custom, model.Preset);
        Assert.Equal("2026-03-14 10:40", model.From);
        Assert.Equal("2026-03-14 10:45", model.To);
        Assert.Equal(["inside"], Messages(model));
    }

    [Fact]
    public async Task Says_Loading_again_while_a_refresh_reads()
    {
        var model = await _services.With(Entry("2026-03-14 10:01", "old")).LoadedAsync(Cancel);
        var release = _services.Source.Hold();

        var load = model.LoadAsync(Cancel);

        Assert.True(model.IsLoading);
        Assert.Equal("Loading…", model.ListMessage);

        release();
        await load;

        Assert.False(model.IsLoading);
    }
}
