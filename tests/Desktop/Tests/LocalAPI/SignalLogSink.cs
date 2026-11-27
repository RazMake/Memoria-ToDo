using Memoria.ToDo.Shared.Fakes;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>
/// A log sink that keeps every entry and lets a test wait for one to arrive, so a test synchronizes on the entry instead of
/// sleeping. The wait only bounds a failure. Shared test setup, not a fake of a port.
/// </summary>
internal sealed class SignalLogSink : ILogEventSink
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly Lock gate = new();
    private readonly List<LogEvent> events = [];
    private readonly List<(Func<LogEvent, bool> Match, TaskCompletionSource<LogEvent> Found)> waiters = [];

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

    public string Describe() =>
        string.Join('\n', Events.Select(entry => $"{entry.Level}: {entry.EverythingIn()}"));

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        List<TaskCompletionSource<LogEvent>> done = [];
        lock (gate)
        {
            events.Add(logEvent);
            foreach (var waiter in waiters.Where(w => w.Match(logEvent)).ToList())
            {
                waiters.Remove(waiter);
                done.Add(waiter.Found);
            }
        }

        foreach (var found in done)
            found.TrySetResult(logEvent);
    }

    /// <summary>The first entry, already written or still to come, at the level or above that names every fact. Fails when none arrives in time.</summary>
    public Task<LogEvent> EntryAsync(LogEventLevel level, params string[] facts) =>
        EntryAsync(entry => entry.Level == level && facts.All(entry.Names));

    /// <summary>The first entry, already written or still to come, at any level that names every fact.</summary>
    public Task<LogEvent> AnyEntryAsync(params string[] facts) => EntryAsync(entry => facts.All(entry.Names));

    private Task<LogEvent> EntryAsync(Func<LogEvent, bool> match)
    {
        TaskCompletionSource<LogEvent> found = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            var existing = events.FirstOrDefault(match);
            if (existing is not null)
                return Task.FromResult(existing);
            waiters.Add((match, found));
        }

        return found.Task.WaitAsync(Bound);
    }
}
