using System.IO;
using Memoria.ToDo.Desktop.Logs;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The app's log source on a real folder: it reads back what the log writer wrote, and files written by hand, as the Logs window
/// reads them. The source is <c>new LogFileSource(folder, () =&gt; writeProblem, logger)</c>.
/// </summary>
[Integration]
public sealed class LogFileSourceTests : IDisposable
{
    private readonly LogFolder _folder = new();
    private readonly InMemoryLogSink _sink = new();
    // The day of the files written by hand. The source reads every log file whatever its date, so a fixed day never goes stale.
    private readonly DateTime _today = new(2026, 3, 14);
    private string? _writeProblem;

    public void Dispose() => _folder.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private LogFileSource Source() => new(_folder.Path, () => _writeProblem, _sink.CreateLogger());

    private Task<LogRead> ReadAsync() => Source().ReadAsync(Cancel);

    // Entries written through the configured writer

    [Fact]
    public async Task Reads_back_an_entry_the_log_wrote_with_its_level_component_message_and_exception()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        using (var log = AppLog.Create(_folder.Path))
        {
            try
            {
                throw new InvalidOperationException("the disk boom");
            }
            catch (InvalidOperationException failure)
            {
                log.ForModule("Sync").Error(failure, "Pushed {Count} files to {Name}", 3, "origin");
            }
        }

        var read = await ReadAsync();

        var entry = Assert.Single(read.Entries);
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.Equal("Sync", entry.Component);
        Assert.Contains("Pushed 3 files to", entry.Message);
        Assert.Contains("origin", entry.Message);
        Assert.Contains("InvalidOperationException", entry.Error);
        Assert.Contains("the disk boom", entry.Error);
        Assert.InRange(entry.Time, before, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(0, read.UnreadableLines);
    }

    [Theory]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public async Task Reads_back_each_level_the_log_writes(LogEventLevel level)
    {
        using (var log = AppLog.Create(_folder.Path))
        {
            log.ForModule("Tasks").Write(level, "An entry");
        }

        var entry = Assert.Single((await ReadAsync()).Entries);

        Assert.Equal(level, entry.Level);
        Assert.Equal("Tasks", entry.Component);
        Assert.Null(entry.Error);
    }

    [Fact]
    public async Task Reads_back_an_entry_without_an_exception_as_having_no_error()
    {
        using (var log = AppLog.Create(_folder.Path))
        {
            log.ForModule("App").Information("Started");
        }

        Assert.Null(Assert.Single((await ReadAsync()).Entries).Error);
    }

    [Fact]
    public async Task Reads_back_every_entry_of_every_numbered_file_of_a_day()
    {
        var text = new string('x', 100_000);
        using (var log = AppLog.Create(_folder.Path))
        {
            for (var number = 0; number < 30; number++)
            {
                log.ForModule("App").Information("Entry {Number} {Text}", number, text);
            }
        }

        var read = await ReadAsync();

        Assert.True(_folder.Names().Length >= 3);
        Assert.Equal(30, read.Entries.Count);
    }

    // Files written by hand

    [Fact]
    public async Task Maps_a_line_to_its_time_level_component_rendered_message_and_exception()
    {
        _folder.Leave(LogFolder.FileName(_today), """
            {"@t":"2026-03-14T12:30:45.1234567Z","@mt":"Pushed {Count} files","Count":3,"@l":"Warning","@x":"System.IO.IOException: boom\n   at Memoria.Push()","SourceContext":"Sync"}

            """);

        var read = await ReadAsync();

        var entry = Assert.Single(read.Entries);
        Assert.Equal(new DateTimeOffset(2026, 3, 14, 12, 30, 45, TimeSpan.Zero).AddTicks(1234567), entry.Time);
        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.Equal("Sync", entry.Component);
        Assert.Equal("Pushed 3 files", entry.Message);
        Assert.Equal("System.IO.IOException: boom\n   at Memoria.Push()", entry.Error);
    }

    [Fact]
    public async Task Shows_an_entry_with_no_SourceContext_as_Unknown()
    {
        _folder.Leave(LogFolder.FileName(_today), """
            {"@t":"2026-03-14T12:30:45.0000000Z","@mt":"No component","@l":"Error"}

            """);

        var entry = Assert.Single((await ReadAsync()).Entries);

        Assert.Equal("Unknown", entry.Component);
        Assert.Equal(LogEventLevel.Error, entry.Level);
    }

    [Fact]
    public async Task Reads_an_entry_with_no_level_as_Information_as_the_compact_format_leaves_it_out()
    {
        _folder.Leave(LogFolder.FileName(_today), """
            {"@t":"2026-03-14T12:30:45.0000000Z","@mt":"Plain","SourceContext":"App"}

            """);

        Assert.Equal(LogEventLevel.Information, Assert.Single((await ReadAsync()).Entries).Level);
    }

    [Fact]
    public async Task Reads_the_entries_of_every_file_in_the_folder()
    {
        _folder.Leave(LogFolder.FileName(_today.AddDays(-2)), """
            {"@t":"2026-03-12T10:00:00.0000000Z","@mt":"From two days ago","SourceContext":"App"}

            """);
        _folder.Leave(LogFolder.FileName(_today.AddDays(-1)), """
            {"@t":"2026-03-13T10:00:00.0000000Z","@mt":"From yesterday","SourceContext":"App"}
            {"@t":"2026-03-13T10:01:00.0000000Z","@mt":"Also yesterday","SourceContext":"App"}

            """);
        _folder.Leave(LogFolder.FileName(_today), """
            {"@t":"2026-03-14T10:00:00.0000000Z","@mt":"From today","SourceContext":"App"}

            """);

        var read = await ReadAsync();

        Assert.Equal(
            ["Also yesterday", "From today", "From two days ago", "From yesterday"],
            read.Entries.Select(entry => entry.Message).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("""{"@t":"2026-03-13T10:00:01.0000000Z","@mt":"Cut off","SourceContext":"App","Sta""")]
    [InlineData("""{"@t":"not-a-time","@mt":"Bad time"}""")]
    [InlineData("""{"@t":"2026-03-13T10:00:01.0000000Z","@mt":"Bad level","@l":"Loud"}""")]
    [InlineData("""{"@t":"2026-03-13T10:00:01.0000000Z","@mt":"Bad level","@l":7}""")]
    [InlineData("""{"@t":13,"@mt":"Bad time"}""")]
    public async Task Counts_a_line_that_fails_to_parse_in_any_way_and_still_reads_the_lines_around_it(string broken)
    {
        _folder.Leave(LogFolder.FileName(_today), $$"""
            {"@t":"2026-03-13T10:00:00.0000000Z","@mt":"Good one","SourceContext":"App"}
            {{broken}}
            {"@t":"2026-03-13T10:00:02.0000000Z","@mt":"Good two","SourceContext":"App"}
            """);

        var read = await ReadAsync();

        Assert.Equal(["Good one", "Good two"], read.Entries.Select(entry => entry.Message).Order(StringComparer.Ordinal));
        Assert.Equal(1, read.UnreadableLines);
    }

    [Fact]
    public async Task Counts_each_broken_line_and_still_reads_the_good_ones()
    {
        _folder.Leave(LogFolder.FileName(_today.AddDays(-1)), """
            {"@t":"2026-03-13T10:00:00.0000000Z","@mt":"Good one","SourceContext":"App"}
            this is not json at all
            {"@t":"2026-03-13T10:00:01.0000000Z","@mt":"Good two","SourceContext":"App"}
            {"@t":"2026-03-13T10:00:02.0000000Z","@mt":"Cut off by a cra
            """);
        _folder.Leave(LogFolder.FileName(_today), """
            {"@mt":"No time"}
            {"@t":"2026-03-14T10:00:00.0000000Z","@mt":"Good three","SourceContext":"App"}

            """);

        var read = await ReadAsync();

        Assert.Equal(3, read.Entries.Count);
        Assert.Equal(3, read.UnreadableLines);
    }

    [Fact]
    public async Task Reads_an_empty_file_as_no_entries_and_no_broken_lines()
    {
        _folder.Leave(LogFolder.FileName(_today), "");

        var read = await ReadAsync();

        Assert.Empty(read.Entries);
        Assert.Equal(0, read.UnreadableLines);
    }

    // Files that can't be read

    [Fact]
    public async Task Leaves_out_a_file_another_process_holds_and_logs_a_Warning_naming_it()
    {
        var held = _folder.Leave(LogFolder.FileName(_today.AddDays(-1)), """
            {"@t":"2026-03-13T10:00:00.0000000Z","@mt":"In the held file","SourceContext":"App"}

            """);
        _folder.Leave(LogFolder.FileName(_today), """
            {"@t":"2026-03-14T10:00:00.0000000Z","@mt":"In the free file","SourceContext":"App"}

            """);

        LogRead read;
        using (new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            read = await ReadAsync();
        }

        Assert.Equal(["In the free file"], read.Entries.Select(entry => entry.Message));
        Assert.Equal(0, read.UnreadableLines);
        _sink.Events.SingleAt(LogEventLevel.Warning, Path.GetFileName(held));
    }

    [Fact]
    public async Task Logs_no_problem_for_files_it_can_read_broken_lines_included()
    {
        _folder.Leave(LogFolder.FileName(_today), """
            {"@t":"2026-03-14T10:00:00.0000000Z","@mt":"Fine","SourceContext":"App"}
            broken
            """);

        await ReadAsync();

        _sink.Events.AssertNoProblems();
    }

    [Fact]
    public async Task Throws_when_the_log_folder_cannot_be_listed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_folder.Path)!);
        File.WriteAllText(_folder.Path, "A file where the log folder should be.");

        await Assert.ThrowsAnyAsync<Exception>(ReadAsync);
    }

    // The writer's problem

    [Fact]
    public async Task Passes_on_the_writers_current_problem()
    {
        _folder.Leave(LogFolder.FileName(_today));
        _writeProblem = "The disk is full";

        Assert.Equal("The disk is full", (await ReadAsync()).WriteProblem);
    }

    [Fact]
    public async Task Reports_no_write_problem_while_the_writer_works()
    {
        _folder.Leave(LogFolder.FileName(_today));

        Assert.Null((await ReadAsync()).WriteProblem);
    }

    [Fact]
    public async Task Asks_for_the_writers_problem_each_time_it_reads_not_once()
    {
        _folder.Leave(LogFolder.FileName(_today));
        var source = Source();
        Assert.Null((await source.ReadAsync(Cancel)).WriteProblem);

        _writeProblem = "Access denied";

        Assert.Equal("Access denied", (await source.ReadAsync(Cancel)).WriteProblem);
    }
}
