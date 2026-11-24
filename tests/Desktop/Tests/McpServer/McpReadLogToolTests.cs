using Memoria.ToDo.Desktop.Logs;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.McpServer.Tests;

/// <summary>The read_log tool: its input rules, which entries it answers and in what order, and what else its answer says.</summary>
[Unit]
public class McpReadLogToolTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static LogEntry Entry(int minute, LogEventLevel level, string component, string message, string? error = null) =>
        new(Noon.AddMinutes(minute), level, component, message, error);

    private static McpHarness WithLog(params LogEntry[] entries)
    {
        var app = new McpHarness();
        app.Logs.Entries.AddRange(entries);
        return app;
    }

    private static string[] Messages(System.Text.Json.JsonElement answer) => [.. answer.Items("entries").Select(entry => entry.Str("message")!)];

    // defaults and order

    [Fact]
    public async Task Answers_the_Information_and_worse_entries_newest_first_by_default()
    {
        await using var app = WithLog(
            Entry(1, LogEventLevel.Information, "Tasks", "oldest"),
            Entry(3, LogEventLevel.Debug, "Tasks", "debug hidden"),
            Entry(2, LogEventLevel.Error, "Sync", "middle"),
            Entry(4, LogEventLevel.Warning, "Tasks", "newest"));

        var answer = await app.OkAsync("read_log");

        Assert.Equal(new[] { "newest", "middle", "oldest" }, Messages(answer));
    }

    [Fact]
    public async Task Answers_each_entry_with_its_local_time_level_component_message_and_error()
    {
        await using var app = WithLog(new LogEntry(Noon.AddMilliseconds(123), LogEventLevel.Error, "Sync", "Push failed", "System.IOException: disk"));

        var answer = await app.OkAsync("read_log");

        var entry = Assert.Single(answer.Items("entries"));
        Assert.Equal("2026-11-23T04:00:00.123-08:00", entry.Str("time"));
        Assert.Equal("Error", entry.Str("level"));
        Assert.Equal("Sync", entry.Str("component"));
        Assert.Equal("Push failed", entry.Str("message"));
        Assert.Equal("System.IOException: disk", entry.Str("error"));
    }

    [Fact]
    public async Task Answers_no_error_for_an_entry_without_one()
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "fine"));

        var answer = await app.OkAsync("read_log");

        Assert.True(answer.Items("entries").Single().IsAbsent("error"));
    }

    [Fact]
    public async Task Answers_how_many_entries_matched_how_many_the_log_holds_and_all_its_components()
    {
        await using var app = WithLog(
            Entry(1, LogEventLevel.Information, "Tasks", "a"),
            Entry(2, LogEventLevel.Debug, "Sync", "b"),
            Entry(3, LogEventLevel.Information, "Contacts", "c"));

        var answer = await app.OkAsync("read_log");

        Assert.Equal(2, answer.GetProperty("matched").GetInt32());
        Assert.Equal(3, answer.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "Contacts", "Sync", "Tasks" }, answer.Strings("components"));
    }

    [Fact]
    public async Task Answers_the_current_time_in_local_time_with_its_offset()
    {
        await using var app = WithLog();

        var answer = await app.OkAsync("read_log");

        Assert.Equal("2026-11-23T12:00:00.000-08:00", answer.Str("now"));
    }

    [Fact]
    public async Task Answers_the_unreadable_lines_and_the_write_problem()
    {
        await using var app = WithLog();
        app.Logs.UnreadableLines = 3;
        app.Logs.WriteProblem = "The log file is locked.";

        var answer = await app.OkAsync("read_log");

        Assert.Equal(3, answer.GetProperty("unreadableLines").GetInt32());
        Assert.Equal("The log file is locked.", answer.Str("writeProblem"));
    }

    [Fact]
    public async Task Answers_no_write_problem_while_the_log_is_written()
    {
        await using var app = WithLog();

        var answer = await app.OkAsync("read_log");

        Assert.True(answer.IsAbsent("writeProblem"));
        Assert.Equal(0, answer.GetProperty("unreadableLines").GetInt32());
    }

    // level

    [Theory]
    [InlineData("debug", 4)]
    [InlineData("DEBUG", 4)]
    [InlineData("Information", 3)]
    [InlineData("warning", 2)]
    [InlineData("Error", 1)]
    [InlineData("fatal", 0)]
    public async Task Takes_a_level_name_in_any_case_and_answers_that_level_and_worse(string level, int expected)
    {
        await using var app = WithLog(
            Entry(1, LogEventLevel.Debug, "Tasks", "debug"),
            Entry(2, LogEventLevel.Information, "Tasks", "information"),
            Entry(3, LogEventLevel.Warning, "Tasks", "warning"),
            Entry(4, LogEventLevel.Error, "Tasks", "error"));

        var answer = await app.OkAsync("read_log", new { level });

        Assert.Equal(expected, answer.Items("entries").Length);
    }

    [Theory]
    [InlineData("Verbose")]
    [InlineData("3")]
    [InlineData("loud")]
    [InlineData("")]
    public async Task Refuses_a_level_that_is_not_a_name_from_Debug_to_Fatal_without_reading_the_log(string level)
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        var error = await app.ErrorAsync("read_log", new { level });

        Assert.Contains("Debug", error);
        Assert.Equal(0, app.Logs.ReadCount);
    }

    [Fact]
    public async Task Refuses_a_level_given_as_a_number()
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        Assert.True(await app.WasRefusedAsync("read_log", new { level = 3 }));
        Assert.Equal(0, app.Logs.ReadCount);
    }

    // components

    [Fact]
    public async Task Answers_only_the_entries_of_the_named_components()
    {
        await using var app = WithLog(
            Entry(1, LogEventLevel.Information, "Tasks", "tasks"),
            Entry(2, LogEventLevel.Information, "Sync", "sync"),
            Entry(3, LogEventLevel.Information, "Contacts", "contacts"));

        var answer = await app.OkAsync("read_log", new { components = new[] { "Tasks", "Contacts" } });

        Assert.Equal(new[] { "contacts", "tasks" }, Messages(answer));
        Assert.Equal(2, answer.GetProperty("matched").GetInt32());
    }

    [Fact]
    public async Task Refuses_an_empty_list_of_components_listing_the_components_the_log_has()
    {
        await using var app = WithLog(
            Entry(1, LogEventLevel.Information, "Tasks", "a"),
            Entry(2, LogEventLevel.Information, "Sync", "b"));

        var error = await app.ErrorAsync("read_log", new { components = Array.Empty<string>() });

        Assert.Contains("Sync", error);
        Assert.Contains("Tasks", error);
    }

    [Fact]
    public async Task Refuses_an_unknown_component_listing_the_components_the_log_has()
    {
        await using var app = WithLog(
            Entry(1, LogEventLevel.Information, "Tasks", "a"),
            Entry(2, LogEventLevel.Information, "Sync", "b"));

        var error = await app.ErrorAsync("read_log", new { components = new[] { "Nothing" } });

        Assert.Contains("Nothing", error);
        Assert.Contains("Sync", error);
        Assert.Contains("Tasks", error);
    }

    [Fact]
    public async Task Compares_component_names_exactly_so_a_different_case_is_unknown()
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        await app.ErrorAsync("read_log", new { components = new[] { "tasks" } });
    }

    // from and to

    [Fact]
    public async Task Answers_entries_from_the_start_time_inclusive_to_the_end_time_exclusive()
    {
        await using var app = WithLog(
            Entry(0, LogEventLevel.Information, "Tasks", "before"),
            Entry(10, LogEventLevel.Information, "Tasks", "at from"),
            Entry(15, LogEventLevel.Information, "Tasks", "inside"),
            Entry(20, LogEventLevel.Information, "Tasks", "at to"));

        var answer = await app.OkAsync("read_log", new { from = "2026-11-23T12:10:00Z", to = "2026-11-23T12:20:00Z" });

        Assert.Equal(new[] { "inside", "at from" }, Messages(answer));
    }

    [Fact]
    public async Task Takes_times_with_any_offset_by_the_moment_they_name()
    {
        await using var app = WithLog(
            Entry(0, LogEventLevel.Information, "Tasks", "early"),
            Entry(30, LogEventLevel.Information, "Tasks", "late"));

        var answer = await app.OkAsync("read_log", new { from = "2026-11-23T04:15:00-08:00" });

        Assert.Equal(new[] { "late" }, Messages(answer));
    }

    [Theory]
    [InlineData("2026-11-23T12:00:00")]
    [InlineData("2026-11-23")]
    [InlineData("yesterday")]
    [InlineData("")]
    public async Task Refuses_a_start_time_that_is_not_ISO_8601_with_an_offset_without_reading_the_log(string from)
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        var error = await app.ErrorAsync("read_log", new { from });

        Assert.Contains("offset", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, app.Logs.ReadCount);
    }

    [Theory]
    [InlineData("2026-11-23T12:00:00")]
    [InlineData("tomorrow")]
    public async Task Refuses_an_end_time_that_is_not_ISO_8601_with_an_offset_without_reading_the_log(string to)
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        await app.ErrorAsync("read_log", new { to });

        Assert.Equal(0, app.Logs.ReadCount);
    }

    [Fact]
    public async Task Refuses_a_start_time_later_than_the_end_time_with_the_filters_message_without_reading_the_log()
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        var error = await app.ErrorAsync("read_log", new { from = "2026-11-23T13:00:00Z", to = "2026-11-23T12:00:00Z" });

        Assert.Contains("From must not be later than To", error);
        Assert.Equal(0, app.Logs.ReadCount);
    }

    // text

    [Fact]
    public async Task Answers_only_the_entries_whose_message_contains_the_text_ignoring_case()
    {
        await using var app = WithLog(
            Entry(1, LogEventLevel.Information, "Tasks", "Added task 7"),
            Entry(2, LogEventLevel.Information, "Tasks", "Deleted task 7"),
            Entry(3, LogEventLevel.Information, "Sync", "Pushed"));

        var answer = await app.OkAsync("read_log", new { text = "DELETED task" });

        Assert.Equal(new[] { "Deleted task 7" }, Messages(answer));
    }

    // limit

    [Fact]
    public async Task Answers_at_most_100_entries_by_default_and_still_counts_all_the_matches()
    {
        await using var app = WithLog([.. Enumerable.Range(0, 150).Select(i => Entry(i, LogEventLevel.Information, "Tasks", $"entry {i}"))]);

        var answer = await app.OkAsync("read_log");

        Assert.Equal(100, answer.Items("entries").Length);
        Assert.Equal("entry 149", Messages(answer)[0]);
        Assert.Equal(150, answer.GetProperty("matched").GetInt32());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(500)]
    public async Task Takes_a_limit_from_1_to_500(int limit)
    {
        await using var app = WithLog([.. Enumerable.Range(0, 600).Select(i => Entry(i, LogEventLevel.Information, "Tasks", $"entry {i}"))]);

        var answer = await app.OkAsync("read_log", new { limit });

        Assert.Equal(limit, answer.Items("entries").Length);
        Assert.Equal("entry 599", Messages(answer)[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public async Task Refuses_a_limit_outside_1_to_500_without_reading_the_log(int limit)
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        var error = await app.ErrorAsync("read_log", new { limit });

        Assert.Contains("500", error);
        Assert.Equal(0, app.Logs.ReadCount);
    }

    [Fact]
    public async Task Refuses_a_limit_that_is_not_a_whole_number()
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        Assert.True(await app.WasRefusedAsync("read_log", new { limit = 2.5 }));
        Assert.Equal(0, app.Logs.ReadCount);
    }

    // failures

    [Fact]
    public async Task Fails_with_the_reason_when_the_log_cannot_be_read()
    {
        await using var app = WithLog();
        app.Logs.Failure = new IOException("The log folder is gone.");

        var error = await app.ErrorAsync("read_log");

        Assert.Contains("The log folder is gone.", error);
    }

    [Fact]
    public async Task Reads_the_log_once_per_call()
    {
        await using var app = WithLog(Entry(1, LogEventLevel.Information, "Tasks", "a"));

        await app.OkAsync("read_log");
        await app.OkAsync("read_log", new { level = "Debug" });

        Assert.Equal(2, app.Logs.ReadCount);
    }
}
