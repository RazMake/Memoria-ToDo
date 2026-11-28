using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The in-memory log sink tests use to read what a module logged: thread-safe, in order, and faithful.</summary>
[Unit]
public sealed class InMemoryLogSinkTests
{
    private readonly InMemoryLogSink sink = new();

    [Fact]
    public void Keeps_every_event_written_from_many_threads_at_once()
    {
        var logger = sink.CreateLogger();

        Parallel.For(0, 8, thread =>
        {
            for (var n = 0; n < 500; n++)
                logger.Information("Thread {Thread} entry {Number}", thread, n);
        });

        Assert.Equal(4000, sink.Events.Count);
    }

    [Fact]
    public void Returns_events_oldest_first()
    {
        var logger = sink.CreateLogger();

        logger.Information("first");
        logger.Warning("second");
        logger.Error("third");

        Assert.Equal(["first", "second", "third"], sink.Events.Select(e => e.RenderMessage()));
    }

    [Fact]
    public void Returns_a_copy_that_later_events_do_not_change()
    {
        var logger = sink.CreateLogger();
        logger.Information("first");
        var taken = sink.Events;

        logger.Information("second");

        Assert.Single(taken);
        Assert.Equal(2, sink.Events.Count);
    }

    [Fact]
    public void Creates_a_logger_that_writes_every_level_from_verbose_to_fatal()
    {
        var logger = sink.CreateLogger();

        logger.Verbose("v");
        logger.Debug("d");
        logger.Information("i");
        logger.Warning("w");
        logger.Error("e");
        logger.Fatal("f");

        Assert.Equal(
            [LogEventLevel.Verbose, LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal],
            sink.Events.Select(e => e.Level));
    }

    [Fact]
    public void Keeps_an_events_exception_and_property_values_unchanged()
    {
        var failure = new InvalidOperationException("Broken.");

        sink.CreateLogger().Error(failure, "Failed {Name} with {Count}", "alpha", 3);

        var entry = Assert.Single(sink.Events);
        Assert.Same(failure, entry.Exception);
        Assert.Equal("alpha", Assert.IsType<ScalarValue>(entry.Properties["Name"]).Value);
        Assert.Equal(3, Assert.IsType<ScalarValue>(entry.Properties["Count"]).Value);
    }
}
