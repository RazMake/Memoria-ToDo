using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// What the writer's failure listener does with a failure that lost no entry: the rolling file sink reports an old log file it
/// couldn't delete as a temporary failure with no entries. Such a file is logged as a Warning, isn't counted as lost entries,
/// and isn't a write problem, since entries still reach the disk. The listener is <c>new LostEntries(time)</c>, and the sink
/// that does the writing is the one it watches.
/// </summary>
[Unit]
public sealed class LostEntriesTests
{
    private const string OldFile = "memoria-20260101.log";

    private readonly InMemoryLogSink _writer = new();
    private readonly LostEntries _lost = new(new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
    private readonly ILogEventSink _watched;

    public LostEntriesTests() => _watched = _lost.Watch(_writer);

    private void OldFileCouldNotBeDeleted() => _lost.OnLoggingFailed(
        this,
        LoggingFailureKind.Temporary,
        $"Could not delete the old log file {OldFile}",
        events: null,
        new IOException($"{OldFile} is in use by another process."));

    private void WriteAnEntry(string message)
    {
        var source = new InMemoryLogSink();
        source.CreateLogger().Information(message);
        _watched.Emit(Assert.Single(source.Events));
    }

    [Fact]
    public void Logs_an_old_file_that_could_not_be_deleted_as_a_Warning_naming_the_file()
    {
        OldFileCouldNotBeDeleted();
        WriteAnEntry("The next entry");

        var warning = Assert.Single(_writer.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Contains(OldFile, warning.EverythingIn());
    }

    [Fact]
    public void Does_not_count_an_old_file_that_could_not_be_deleted_as_lost_entries()
    {
        OldFileCouldNotBeDeleted();
        WriteAnEntry("The next entry");

        Assert.Null(_lost.Problem);
    }

    [Fact]
    public void Does_not_report_an_old_file_that_could_not_be_deleted_as_a_write_problem_before_any_entry_is_written()
    {
        OldFileCouldNotBeDeleted();

        Assert.Null(_lost.Problem);
    }

    [Fact]
    public void Logs_the_Warning_for_an_old_file_once_and_writes_the_entries_around_it_whole()
    {
        WriteAnEntry("Before");
        OldFileCouldNotBeDeleted();
        WriteAnEntry("After");
        WriteAnEntry("Much later");

        Assert.Single(_writer.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Equal(1, _writer.Events.Count(e => e.Names("Before")));
        Assert.Equal(1, _writer.Events.Count(e => e.Names("After")));
        Assert.Equal(1, _writer.Events.Count(e => e.Names("Much later")));
    }

    [Fact]
    public void Logs_a_Warning_for_each_old_file_that_could_not_be_deleted()
    {
        OldFileCouldNotBeDeleted();
        OldFileCouldNotBeDeleted();
        WriteAnEntry("The next entry");

        Assert.Equal(2, _writer.Events.Count(e => e.Level == LogEventLevel.Warning));
    }

    [Fact]
    public void Still_counts_entries_a_permanent_failure_lost_beside_an_old_file_that_could_not_be_deleted()
    {
        var lostEntry = new InMemoryLogSink();
        lostEntry.CreateLogger().Information("Lost one");

        OldFileCouldNotBeDeleted();
        _lost.OnLoggingFailed(this, LoggingFailureKind.Permanent, "The disk is full", [.. lostEntry.Events], new IOException("The disk is full."));

        Assert.NotNull(_lost.Problem);
    }
}
