using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>What one row of the list shows, what selecting it shows below the list, and the text Ctrl+C copies.</summary>
[Unit]
public class LogRowsTests
{
    private readonly LogsServices _services = new();

    private async Task<LogRow> OneRowAsync(LogEntry entry)
    {
        var model = await _services.With(entry).LoadedAsync(TestContext.Current.CancellationToken);
        return Assert.Single(model.Rows);
    }

    [Fact]
    public async Task Shows_the_time_in_this_PCs_local_zone_with_the_date_and_milliseconds()
    {
        var row = await OneRowAsync(LogsServices.Entry("2026-03-14 14:05:06.078"));

        Assert.Contains("2026", row.TimeText);
        Assert.Contains("14:05:06.078", row.TimeText);
    }

    [Theory]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public async Task Shows_the_severity_as_the_levels_name(LogEventLevel level)
    {
        var row = await OneRowAsync(LogsServices.Entry("2026-03-14 14:00", level));

        Assert.Equal(level.ToString(), row.SeverityText);
    }

    [Fact]
    public async Task Shows_the_component_and_only_the_first_line_of_the_message()
    {
        var row = await OneRowAsync(LogsServices.Entry("2026-03-14 14:00", component: "Tasks", message: "Saved task 7\nsecond line\r\nthird line"));

        Assert.Equal("Tasks", row.Component);
        Assert.Equal("Saved task 7", row.Message);
    }

    [Fact]
    public async Task Marks_a_row_that_has_an_exception_and_not_one_that_has_none()
    {
        var model = await _services
            .With(
                LogsServices.Entry("2026-03-14 14:00", message: "Failed", error: "System.IO.IOException: boom"),
                LogsServices.Entry("2026-03-14 13:00", message: "Fine"))
            .LoadedAsync(TestContext.Current.CancellationToken);

        Assert.Equal([true, false], model.Rows.Select(row => row.HasError));
    }

    [Fact]
    public async Task Shows_the_whole_message_and_the_exceptions_text_when_the_row_is_selected()
    {
        var row = await OneRowAsync(LogsServices.Entry(
            "2026-03-14 14:00", message: "Saved task 7\nsecond line", error: "System.IO.IOException: boom\n   at Memoria.Save()"));

        Assert.Contains("Saved task 7", row.Details);
        Assert.Contains("second line", row.Details);
        Assert.Contains("System.IO.IOException: boom", row.Details);
        Assert.Contains("at Memoria.Save()", row.Details);
    }

    [Fact]
    public async Task Shows_just_the_message_for_a_row_without_an_exception()
    {
        var row = await OneRowAsync(LogsServices.Entry("2026-03-14 14:00", message: "Just this"));

        Assert.Equal("Just this", row.Details.Trim());
    }

    [Fact]
    public async Task Copies_each_selected_row_as_a_line_of_time_severity_component_and_message()
    {
        var model = await _services
            .With(
                LogsServices.Entry("2026-03-14 14:00:01.250", LogEventLevel.Warning, "Sync", "Offline"),
                LogsServices.Entry("2026-03-14 14:00:02.500", LogEventLevel.Information, "Tasks", "Saved"))
            .LoadedAsync(TestContext.Current.CancellationToken);

        var lines = model.CopyText(model.Rows).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(2, lines.Length);
        AssertInOrder(lines[0], "14:00:02.500", "Information", "Tasks", "Saved");
        AssertInOrder(lines[1], "14:00:01.250", "Warning", "Sync", "Offline");
    }

    [Fact]
    public async Task Copies_the_rows_in_the_order_given_with_an_exception_after_its_line()
    {
        var model = await _services
            .With(
                LogsServices.Entry("2026-03-14 14:00:02", LogEventLevel.Error, "Sync", "Pass failed", "System.IO.IOException: boom\n   at Memoria.Pass()"),
                LogsServices.Entry("2026-03-14 14:00:01", LogEventLevel.Information, "Tasks", "Saved"))
            .LoadedAsync(TestContext.Current.CancellationToken);

        var text = model.CopyText([model.Rows[1], model.Rows[0]]);

        Assert.True(
            text.IndexOf("Saved", StringComparison.Ordinal) < text.IndexOf("Pass failed", StringComparison.Ordinal),
            "The rows are copied in the order given.");
        Assert.True(
            text.IndexOf("Pass failed", StringComparison.Ordinal) < text.IndexOf("System.IO.IOException: boom", StringComparison.Ordinal),
            "The exception follows its row.");
        Assert.Contains("at Memoria.Pass()", text);
    }

    [Fact]
    public async Task Copies_nothing_when_no_row_is_selected()
    {
        var model = await _services.With(LogsServices.Entry("2026-03-14 14:00")).LoadedAsync(TestContext.Current.CancellationToken);

        Assert.Equal("", model.CopyText([]).Trim());
    }

    private static void AssertInOrder(string line, params string[] parts)
    {
        var from = 0;
        foreach (var part in parts)
        {
            var at = line.IndexOf(part, from, StringComparison.Ordinal);
            Assert.True(at >= 0, $"'{part}' should follow the earlier parts in: {line}");
            from = at + part.Length;
        }
    }
}
