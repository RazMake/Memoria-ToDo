using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>The public <see cref="LogFilter"/>: which entries match, which filters it refuses, and the components it lists.</summary>
[Unit]
public class LogFilterTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

    private static LogEntry Entry(
        LogEventLevel level = LogEventLevel.Information, string component = "Sync", DateTimeOffset? time = null,
        string message = "A message", string? error = null) =>
        new(time ?? Noon, level, component, message, error);

    private static LogFilter Filter(
        LogEventLevel level = LogEventLevel.Debug, IEnumerable<string>? components = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null, string? text = null) =>
        new(level, components, from, to, text);

    // Levels

    [Fact]
    public void Lists_exactly_the_five_levels_lowest_first()
    {
        Assert.Equal(
            [LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal],
            LogFilter.Levels);
    }

    [Theory]
    [InlineData(LogEventLevel.Debug, LogEventLevel.Debug, true)]
    [InlineData(LogEventLevel.Information, LogEventLevel.Debug, false)]
    [InlineData(LogEventLevel.Information, LogEventLevel.Information, true)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Information, false)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Error, true)]
    [InlineData(LogEventLevel.Error, LogEventLevel.Warning, false)]
    [InlineData(LogEventLevel.Fatal, LogEventLevel.Error, false)]
    [InlineData(LogEventLevel.Fatal, LogEventLevel.Fatal, true)]
    public void Matches_an_entry_at_the_minimum_level_or_above_only(LogEventLevel minimum, LogEventLevel entryLevel, bool expected)
    {
        Assert.Equal(expected, Filter(level: minimum).Matches(Entry(entryLevel)));
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData((LogEventLevel)99)]
    [InlineData((LogEventLevel)(-1))]
    public void Rejects_a_minimum_level_that_is_not_one_of_the_five(LogEventLevel minimum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Filter(level: minimum));
    }

    // Components

    [Fact]
    public void Matches_every_component_without_a_list()
    {
        var filter = Filter(components: null);

        Assert.True(filter.Matches(Entry(component: "Sync")));
        Assert.True(filter.Matches(Entry(component: "Unknown")));
    }

    [Fact]
    public void Matches_only_the_components_in_the_list()
    {
        var filter = Filter(components: ["Sync", "Tasks"]);

        Assert.True(filter.Matches(Entry(component: "Sync")));
        Assert.True(filter.Matches(Entry(component: "Tasks")));
        Assert.False(filter.Matches(Entry(component: "Platform")));
    }

    [Fact]
    public void Compares_component_names_exactly_including_case()
    {
        var filter = Filter(components: ["sync"]);

        Assert.False(filter.Matches(Entry(component: "Sync")));
        Assert.True(filter.Matches(Entry(component: "sync")));
    }

    [Fact]
    public void Matches_no_entry_with_an_empty_component_list()
    {
        Assert.False(Filter(components: []).Matches(Entry()));
    }

    [Fact]
    public void Rejects_a_null_name_in_the_component_list()
    {
        Assert.Throws<ArgumentNullException>(() => Filter(components: ["Sync", null!]));
    }

    // From and To

    [Fact]
    public void Matches_an_entry_at_From_but_not_one_tick_before()
    {
        var filter = Filter(from: Noon);

        Assert.True(filter.Matches(Entry(time: Noon)));
        Assert.False(filter.Matches(Entry(time: Noon.AddTicks(-1))));
    }

    [Fact]
    public void Matches_an_entry_one_tick_before_To_but_not_one_at_To()
    {
        var filter = Filter(to: Noon);

        Assert.True(filter.Matches(Entry(time: Noon.AddTicks(-1))));
        Assert.False(filter.Matches(Entry(time: Noon)));
    }

    [Fact]
    public void Sets_no_limit_for_a_missing_bound()
    {
        var early = Entry(time: DateTimeOffset.MinValue);
        var late = Entry(time: DateTimeOffset.MaxValue);

        Assert.True(Filter(from: null, to: Noon).Matches(early));
        Assert.True(Filter(from: Noon, to: null).Matches(late));
        Assert.True(Filter(from: null, to: null).Matches(early));
        Assert.True(Filter(from: null, to: null).Matches(late));
    }

    [Fact]
    public void Compares_the_bounds_as_moments_whatever_their_offsets()
    {
        var from = new DateTimeOffset(2026, 3, 14, 14, 0, 0, TimeSpan.FromHours(2));
        var to = new DateTimeOffset(2026, 3, 14, 8, 0, 0, TimeSpan.FromHours(-5));
        var filter = Filter(from: from, to: to);

        Assert.True(filter.Matches(Entry(time: new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero))));
        Assert.True(filter.Matches(Entry(time: new DateTimeOffset(2026, 3, 14, 17, 59, 59, TimeSpan.FromHours(5)))));
        Assert.False(filter.Matches(Entry(time: new DateTimeOffset(2026, 3, 14, 11, 59, 59, TimeSpan.Zero))));
        Assert.False(filter.Matches(Entry(time: new DateTimeOffset(2026, 3, 14, 15, 0, 0, TimeSpan.FromHours(5)))));
    }

    [Fact]
    public void Rejects_a_From_later_than_To()
    {
        Assert.Throws<ArgumentException>(() => Filter(from: Noon.AddTicks(1), to: Noon));
    }

    [Fact]
    public void Rejects_a_From_that_is_later_than_To_only_as_moments()
    {
        var from = new DateTimeOffset(2026, 3, 14, 12, 0, 1, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 3, 14, 14, 0, 0, TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => Filter(from: from, to: to));
    }

    [Fact]
    public void Allows_a_From_equal_to_To_and_it_matches_nothing()
    {
        var filter = Filter(from: Noon, to: Noon);

        Assert.False(filter.Matches(Entry(time: Noon)));
        Assert.False(filter.Matches(Entry(time: Noon.AddTicks(-1))));
        Assert.False(filter.Matches(Entry(time: Noon.AddTicks(1))));
    }

    // Text

    [Fact]
    public void Matches_text_in_the_message_ignoring_case()
    {
        var filter = Filter(text: "PUSHED 3");

        Assert.True(filter.Matches(Entry(message: "Pushed 3 files")));
        Assert.False(filter.Matches(Entry(message: "Fetched nothing")));
    }

    [Fact]
    public void Matches_text_on_any_line_of_the_message()
    {
        var filter = Filter(text: "needle");

        Assert.True(filter.Matches(Entry(message: "First line\nsecond line has Needle")));
    }

    [Fact]
    public void Matches_text_in_the_exception_only_ignoring_case()
    {
        var filter = Filter(text: "ioexception");

        Assert.True(filter.Matches(Entry(message: "Pass failed", error: "System.IO.IOException: disk full")));
        Assert.False(filter.Matches(Entry(message: "Pass failed", error: "System.InvalidOperationException")));
        Assert.False(filter.Matches(Entry(message: "Pass failed", error: null)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Matches_every_entry_for_missing_or_empty_text(string? text)
    {
        var filter = Filter(text: text);

        Assert.True(filter.Matches(Entry(message: "", error: null)));
        Assert.True(filter.Matches(Entry(message: "Anything", error: "Anything else")));
    }

    // Together

    [Fact]
    public void Matches_only_when_all_four_parts_match()
    {
        var filter = Filter(LogEventLevel.Warning, ["Sync"], Noon, Noon.AddHours(1), "push");
        var matching = Entry(LogEventLevel.Warning, "Sync", Noon.AddMinutes(1), "retry the push");

        Assert.True(filter.Matches(matching));
        Assert.False(filter.Matches(matching with { Level = LogEventLevel.Information }));
        Assert.False(filter.Matches(matching with { Component = "Tasks" }));
        Assert.False(filter.Matches(matching with { Time = Noon.AddHours(2) }));
        Assert.False(filter.Matches(matching with { Message = "retry the pull" }));
    }

    [Fact]
    public void Rejects_a_null_entry()
    {
        Assert.Throws<ArgumentNullException>(() => Filter().Matches(null!));
    }

    // ComponentsOf

    [Fact]
    public void Lists_each_component_once_sorted_by_name_ignoring_case_including_Unknown()
    {
        var names = LogFilter.ComponentsOf(
        [
            Entry(component: "tasks"),
            Entry(component: "Sync"),
            Entry(component: "Unknown"),
            Entry(component: "sync2"),
            Entry(component: "Sync"),
            Entry(component: "Platform"),
            Entry(component: "tasks"),
        ]);

        Assert.Equal(["Platform", "Sync", "sync2", "tasks", "Unknown"], names);
    }

    [Fact]
    public void Lists_no_components_for_no_entries()
    {
        Assert.Empty(LogFilter.ComponentsOf([]));
    }

    [Fact]
    public void Rejects_null_entries_when_listing_components()
    {
        Assert.Throws<ArgumentNullException>(() => LogFilter.ComponentsOf(null!));
    }
}
