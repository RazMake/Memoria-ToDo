using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// Counts the entries the log writer lost, whether its queue was full or the writing failed, and says so in a Warning
/// written right after the first entry that writes once writing works again. It hears the losses as Serilog's
/// failure listener, and watches the writes by wrapping the sink that does them.
/// </summary>
internal sealed class LostEntries(TimeProvider time) : ILoggingFailureListener
{
    private static readonly MessageTemplate WarningTemplate =
        new MessageTemplateParser().Parse("{Lost} log entries were lost since {Since} because {Reason}");

    private static readonly MessageTemplate UndeletedTemplate =
        new MessageTemplateParser().Parse("An old log file could not be deleted: {Problem}");

    private readonly object _gate = new();
    private readonly Queue<string> _undeletedFiles = new();
    private long _lost;
    private DateTimeOffset _since;
    private string _reason = "";

    // Set by the one writer thread around each write, so a failure reported on it belongs to that write.
    private volatile int _writingThread;
    private bool _writeFailed;

    /// <summary>Why entries are being lost now, or null while none are.</summary>
    public string? Problem
    {
        get
        {
            lock (_gate)
            {
                return _lost > 0 ? _reason : null;
            }
        }
    }

    public void OnLoggingFailed(
        object sender, LoggingFailureKind kind, string message, IReadOnlyCollection<LogEvent>? events, Exception? exception)
    {
        // A temporary failure with no entries is the rolling sink failing to delete an old file: entries still reach the disk,
        // so it loses nothing and isn't a write problem, but it is worth a Warning once writing next works.
        if (kind == LoggingFailureKind.Temporary && events is null)
        {
            lock (_gate)
            {
                _undeletedFiles.Enqueue(exception is null ? message : $"{message} ({exception.Message})");
            }

            return;
        }

        // Any other temporary failure is retried and loses nothing; a permanent one names the entries it lost.
        if (kind == LoggingFailureKind.Temporary || events is not { Count: > 0 })
        {
            return;
        }

        if (Environment.CurrentManagedThreadId == _writingThread)
        {
            _writeFailed = true;
        }

        Lose(events.Count, exception?.Message ?? message, since: null);
    }

    /// <summary>Wraps the sink that does the writing, so the first good write after a loss is followed by the Warning.</summary>
    public ILogEventSink Watch(ILogEventSink writer) => new WatchedWriter(this, writer);

    private void Lose(long count, string reason, DateTimeOffset? since)
    {
        var first = since ?? time.GetUtcNow();
        lock (_gate)
        {
            if (_lost == 0 || first < _since)
            {
                _since = first;
            }

            _lost += count;
            _reason = reason;
        }
    }

    // Writes on the writer thread; true when the entry reached the disk. A throwing write passes on to Serilog's queue,
    // which reports the entry as lost.
    private bool Write(ILogEventSink writer, LogEvent entry)
    {
        _writeFailed = false;
        _writingThread = Environment.CurrentManagedThreadId;
        try
        {
            writer.Emit(entry);
        }
        catch
        {
            _writeFailed = true;
            throw;
        }
        finally
        {
            _writingThread = 0;
        }

        return !_writeFailed;
    }

    private void SayWhatWasLost(ILogEventSink writer)
    {
        long lost;
        DateTimeOffset since;
        string reason;
        lock (_gate)
        {
            if (_lost == 0)
            {
                return;
            }

            (lost, since, reason) = (_lost, _since, _reason);
            _lost = 0;
        }

        var warning = new LogEvent(
            time.GetUtcNow(),
            LogEventLevel.Warning,
            null,
            WarningTemplate,
            [
                new LogEventProperty(Constants.SourceContextPropertyName, new ScalarValue("App")),
                new LogEventProperty("Lost", new ScalarValue(lost)),
                new LogEventProperty("Since", new ScalarValue(since)),
                new LogEventProperty("Reason", new ScalarValue(reason)),
            ]);
        try
        {
            if (Write(writer, warning))
            {
                return;
            }
        }
        catch (Exception failure)
        {
            reason = failure.Message;
        }

        Lose(lost, reason, since);
    }

    // Writes a Warning for each old file the sink couldn't delete since the last good write. A Warning that can't be
    // written stays for the next write and ends this pass.
    private void SayWhichFilesStayed(ILogEventSink writer)
    {
        while (true)
        {
            string problem;
            lock (_gate)
            {
                if (!_undeletedFiles.TryPeek(out var next))
                {
                    return;
                }

                problem = next;
            }

            var warning = new LogEvent(
                time.GetUtcNow(),
                LogEventLevel.Warning,
                null,
                UndeletedTemplate,
                [
                    new LogEventProperty(Constants.SourceContextPropertyName, new ScalarValue("App")),
                    new LogEventProperty("Problem", new ScalarValue(problem)),
                ]);
            try
            {
                if (!Write(writer, warning))
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }

            lock (_gate)
            {
                _undeletedFiles.Dequeue();
            }
        }
    }

    private sealed class WatchedWriter(LostEntries owner, ILogEventSink writer) : ILogEventSink, IDisposable
    {
        public void Emit(LogEvent logEvent)
        {
            if (owner.Write(writer, logEvent))
            {
                owner.SayWhatWasLost(writer);
                owner.SayWhichFilesStayed(writer);
            }
        }

        public void Dispose() => (writer as IDisposable)?.Dispose();
    }
}
