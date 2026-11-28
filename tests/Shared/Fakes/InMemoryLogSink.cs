using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>Keeps every log entry written to it, in order, for tests to read. Safe to write from any thread.</summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private readonly Lock gate = new();
    private readonly List<LogEvent> events = [];

    /// <summary>A copy of the entries written so far, oldest first.</summary>
    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (gate)
                return [.. events];
        }
    }

    /// <summary>A logger that writes only to this sink, at every level.</summary>
    public ILogger CreateLogger() => new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(this).CreateLogger();

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        lock (gate)
            events.Add(logEvent);
    }
}
