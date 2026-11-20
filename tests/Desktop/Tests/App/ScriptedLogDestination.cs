using Serilog.Core;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The writer a log pipeline test hands the pipeline in place of the log file. A test can hold it, so it stops in its
/// first write like a stuck disk, or make it fail, like a full disk, and can wait for the writes it has seen. Every
/// wait is bounded, so a pipeline that never writes fails the test instead of hanging it.
/// </summary>
internal sealed class ScriptedLogDestination : ILogEventSink
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    private readonly object _gate = new();
    private readonly List<LogEvent> _written = [];
    private readonly ManualResetEventSlim _open = new(initialState: true);
    private int _attempts;
    private int _failuresLeft;
    private Exception? _failure;

    /// <summary>The entries written so far, oldest first. Entries whose write failed aren't in it.</summary>
    public IReadOnlyList<LogEvent> Written
    {
        get
        {
            lock (_gate)
            {
                return [.. _written];
            }
        }
    }

    /// <summary>From now on every write waits until <see cref="Release"/>, so the writer is stuck.</summary>
    public void Hold() => _open.Reset();

    /// <summary>Lets every waiting and later write go on.</summary>
    public void Release() => _open.Set();

    /// <summary>From now on every write throws <paramref name="failure"/>, until <see cref="Recover"/>.</summary>
    public void FailWith(Exception failure) => FailNext(failure, int.MaxValue);

    /// <summary>The next <paramref name="count"/> writes throw <paramref name="failure"/>, then writes succeed again.</summary>
    public void FailNext(Exception failure, int count)
    {
        lock (_gate)
        {
            _failure = failure;
            _failuresLeft = count;
        }
    }

    /// <summary>From now on writes succeed again.</summary>
    public void Recover() => FailNext(new InvalidOperationException("Not used."), 0);

    /// <summary>Waits until the writer has started <paramref name="count"/> writes in all, whether they ended or are held.</summary>
    public void WaitForAttempts(int count) => WaitUntil(() => _attempts >= count, $"{count} writes started");

    /// <summary>Waits until <paramref name="count"/> entries in all have been written.</summary>
    public void WaitForWritten(int count) => WaitUntil(() => _written.Count >= count, $"{count} entries written");

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            _attempts++;
            Monitor.PulseAll(_gate);
        }

        _open.Wait();
        lock (_gate)
        {
            if (_failuresLeft > 0)
            {
                _failuresLeft--;
                throw _failure!;
            }

            _written.Add(logEvent);
            Monitor.PulseAll(_gate);
        }
    }

    private void WaitUntil(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Limit;
        lock (_gate)
        {
            while (!condition())
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero || !Monitor.Wait(_gate, left))
                {
                    throw new TimeoutException($"The writer didn't see {what} within {Limit}.");
                }
            }
        }
    }
}
