using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>The minimum-level list, the component list, and the quick search, each alone and then together.</summary>
[Unit]
public class LogsFilterTests
{
    private readonly LogsServices _services = new();

    private CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string[] Messages(LogsModel model) => [.. model.Rows.Select(row => row.Message)];

    private Task<LogsModel> LoadedAsync(params LogEntry[] entries) => _services.With(entries).LoadedAsync(Cancel);

    private static LogEntry Entry(int minute, LogEventLevel level, string component, string message, string? error = null) =>
        LogsServices.Entry($"2026-03-14 10:{minute:D2}", level, component, message, error);

    private Task<LogsModel> LoadedOneOfEachLevelAsync() => LoadedAsync(
        Entry(5, LogEventLevel.Debug, "Sync", "debug"),
        Entry(4, LogEventLevel.Information, "Sync", "info"),
        Entry(3, LogEventLevel.Warning, "Sync", "warning"),
        Entry(2, LogEventLevel.Error, "Sync", "error"),
        Entry(1, LogEventLevel.Fatal, "Sync", "fatal"));

    // Severity

    [Fact]
    public async Task Offers_the_four_minimum_levels_Information_and_up_first()
    {
        var model = await LoadedOneOfEachLevelAsync();

        Assert.Equal(
            ["Information and up", "Warning and up", "Error and up", "Fatal only"],
            model.Levels.Select(choice => choice.Label));
        Assert.Equal(
            [LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal],
            model.Levels.Select(choice => choice.Level));
    }

    [Fact]
    public async Task Shows_Information_and_up_by_default()
    {
        var model = await LoadedOneOfEachLevelAsync();

        Assert.Equal(LogEventLevel.Information, model.MinimumLevel);
        Assert.Equal(["info", "warning", "error", "fatal"], Messages(model));
        Assert.DoesNotContain("debug", Messages(model));
    }

    [Theory]
    [InlineData(LogEventLevel.Warning, new[] { "warning", "error", "fatal" })]
    [InlineData(LogEventLevel.Error, new[] { "error", "fatal" })]
    [InlineData(LogEventLevel.Fatal, new[] { "fatal" })]
    public async Task A_minimum_level_hides_the_entries_below_it(LogEventLevel minimum, string[] shown)
    {
        var model = await LoadedOneOfEachLevelAsync();

        model.MinimumLevel = minimum;

        Assert.Equal(shown, Messages(model));
    }

    // Component

    [Fact]
    public async Task Lists_the_components_of_the_loaded_entries_in_name_order_all_checked()
    {
        var model = await LoadedAsync(
            Entry(3, LogEventLevel.Information, "Tasks", "a"),
            Entry(2, LogEventLevel.Information, "Sync", "b"),
            Entry(1, LogEventLevel.Information, "Platform", "c"),
            Entry(0, LogEventLevel.Information, "Sync", "d"));

        Assert.Equal(["Platform", "Sync", "Tasks"], model.Components.Select(choice => choice.Name));
        Assert.All(model.Components, choice => Assert.True(choice.IsChecked));
        Assert.True(model.AllComponentsChecked);
    }

    [Fact]
    public async Task Lists_the_Unknown_component_like_any_other()
    {
        var model = await LoadedAsync(
            Entry(1, LogEventLevel.Information, "Unknown", "no component"),
            Entry(0, LogEventLevel.Information, "Sync", "with one"));

        Assert.Equal(["Sync", "Unknown"], model.Components.Select(choice => choice.Name));
    }

    [Fact]
    public async Task Lists_a_component_even_when_another_filter_hides_all_its_entries()
    {
        var model = await LoadedAsync(
            Entry(1, LogEventLevel.Debug, "Quiet", "hidden by the level"),
            Entry(0, LogEventLevel.Information, "Sync", "shown"));

        Assert.Equal(["Quiet", "Sync"], model.Components.Select(choice => choice.Name));
    }

    [Fact]
    public async Task Unchecking_a_component_hides_its_entries_and_checking_it_shows_them_again()
    {
        var model = await LoadedAsync(
            Entry(2, LogEventLevel.Information, "Tasks", "task entry"),
            Entry(1, LogEventLevel.Information, "Sync", "sync entry"));
        var sync = model.Components.Single(choice => choice.Name == "Sync");

        sync.IsChecked = false;

        Assert.Equal(["task entry"], Messages(model));
        Assert.False(model.AllComponentsChecked);

        sync.IsChecked = true;

        Assert.Equal(["task entry", "sync entry"], Messages(model));
        Assert.True(model.AllComponentsChecked);
    }

    [Fact]
    public async Task The_check_all_box_clears_every_component_and_then_checks_them_all()
    {
        var model = await LoadedAsync(
            Entry(2, LogEventLevel.Information, "Tasks", "task entry"),
            Entry(1, LogEventLevel.Information, "Sync", "sync entry"));

        model.AllComponentsChecked = false;

        Assert.All(model.Components, choice => Assert.False(choice.IsChecked));
        Assert.Empty(model.Rows);

        model.AllComponentsChecked = true;

        Assert.All(model.Components, choice => Assert.True(choice.IsChecked));
        Assert.Equal(2, model.Rows.Count);
    }

    // Quick search

    [Fact]
    public async Task Search_matches_part_of_the_message_ignoring_case()
    {
        var model = await LoadedAsync(
            Entry(2, LogEventLevel.Information, "Sync", "Pushed 3 files"),
            Entry(1, LogEventLevel.Information, "Sync", "Fetched nothing"));

        model.SearchText = "PUSHED 3";

        Assert.Equal(["Pushed 3 files"], Messages(model));
    }

    [Fact]
    public async Task Search_finds_text_that_is_only_in_the_exception()
    {
        var model = await LoadedAsync(
            Entry(2, LogEventLevel.Error, "Sync", "Pass failed", "System.IO.IOException: The disk is full\n   at Memoria.Tasks.Save(String tasks.json)"),
            Entry(1, LogEventLevel.Error, "Sync", "Other failure", "System.InvalidOperationException: nope"));

        model.SearchText = "ioexception";
        Assert.Equal(["Pass failed"], Messages(model));

        model.SearchText = "tasks.JSON";
        Assert.Equal(["Pass failed"], Messages(model));
    }

    [Fact]
    public async Task Search_finds_text_on_a_later_line_of_the_message()
    {
        var model = await LoadedAsync(
            Entry(2, LogEventLevel.Information, "Sync", "First line\nsecond line has needle"),
            Entry(1, LogEventLevel.Information, "Sync", "Plain"));

        model.SearchText = "needle";

        Assert.Equal(["First line"], Messages(model));
    }

    [Fact]
    public async Task Search_shows_nothing_when_nothing_contains_the_text()
    {
        var model = await LoadedAsync(Entry(1, LogEventLevel.Information, "Sync", "Plain"));

        model.SearchText = "absent";

        Assert.Empty(model.Rows);
    }

    [Fact]
    public async Task An_empty_search_matches_every_row()
    {
        var model = await LoadedAsync(
            Entry(2, LogEventLevel.Information, "Sync", "one"),
            Entry(1, LogEventLevel.Information, "Sync", "two"));
        model.SearchText = "one";

        model.SearchText = "";

        Assert.Equal(["one", "two"], Messages(model));
        Assert.Equal(2, model.Rows.Count);
    }

    // Together

    [Fact]
    public async Task A_row_shows_only_when_the_time_range_the_level_the_component_and_the_search_all_match()
    {
        var model = await LoadedAsync(
            Entry(30, LogEventLevel.Warning, "Sync", "retry the push"),
            Entry(29, LogEventLevel.Warning, "Sync", "retry the pull"),
            Entry(28, LogEventLevel.Information, "Sync", "retry the push, info"),
            Entry(27, LogEventLevel.Warning, "Tasks", "retry the push, tasks"),
            LogsServices.Entry("2026-03-14 09:59", LogEventLevel.Warning, "Sync", "retry the push, too early"));

        model.From = "2026-03-14 10:00";
        model.MinimumLevel = LogEventLevel.Warning;
        model.Components.Single(choice => choice.Name == "Tasks").IsChecked = false;
        model.SearchText = "push";

        Assert.Equal(["retry the push"], Messages(model));
    }
}
