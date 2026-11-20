using Serilog;
using Serilog.Configuration;
using Serilog.Core;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The logger the composition root makes: it keeps Information and up, hands each module a logger carrying the
/// module's name as its component, and writes through a queue of 10,000 entries on a background thread, so a caller
/// never waits for the writer. Entries the writer loses are counted and reported by <see cref="LostEntries"/>.
/// </summary>
internal sealed class LogPipeline : IDisposable
{
    private const int QueueSize = 10_000;
    private static readonly TimeSpan FlushLimit = TimeSpan.FromSeconds(2);

    private readonly Logger _logger;
    private readonly LostEntries _lost;
    private int _disposed;

    private LogPipeline(Action<LoggerSinkConfiguration> writeTo, TimeProvider time)
    {
        _lost = new LostEntries(time);
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Fallible(
                sinks => sinks.Async(
                    queue => queue.Sink(LoggerSinkConfiguration.Wrap(_lost.Watch, writeTo)),
                    QueueSize,
                    blockWhenFull: false),
                _lost)
            .CreateLogger();
    }

    /// <summary>Why entries can't be written now, or null while they can.</summary>
    public string? WriteProblem => _lost.Problem;

    /// <summary>A pipeline that writes to the destination. A destination whose Emit throws counts as a write failure.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static LogPipeline Create(ILogEventSink destination, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return CreateWriting(sinks => sinks.Sink(destination), time);
    }

    /// <summary>A pipeline that writes to the sinks <paramref name="writeTo"/> adds, which report their failures to Serilog's failure listener.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static LogPipeline CreateWriting(Action<LoggerSinkConfiguration> writeTo, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(writeTo);
        ArgumentNullException.ThrowIfNull(time);
        return new LogPipeline(writeTo, time);
    }

    /// <summary>A logger whose entries carry <paramref name="module"/> as their component.</summary>
    public ILogger ForModule(string module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        return _logger.ForContext(Constants.SourceContextPropertyName, module);
    }

    /// <summary>Writes the queued entries, waiting at most 2 seconds; entries still waiting then are lost. Safe to call twice.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Task.Run(_logger.Dispose).Wait(FlushLimit);
        }
    }
}
