using Memoria.ToDo.Desktop.Logs;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="ILogSource"/>, for Logs' tests. A read answers what <see cref="Entries"/>,
/// <see cref="UnreadableLines"/>, and <see cref="WriteProblem"/> held when it started, in the order the test put the entries
/// (the real source gives no order), or throws <see cref="Failure"/>. <see cref="Hold"/> keeps reads open until released.
/// </summary>
public sealed class FakeLogSource : ILogSource
{
    private readonly object _gate = new();
    private readonly List<int> _readThreads = [];
    private readonly List<(int Count, TaskCompletionSource Done)> _waiters = [];
    private TaskCompletionSource? _hold;
    private int _finished;

    /// <summary>The entries the next read finds.</summary>
    public List<LogEntry> Entries { get; } = [];

    /// <summary>What the next read reports as broken lines.</summary>
    public int UnreadableLines { get; set; }

    /// <summary>What the next read reports as the writer's problem; null when it writes.</summary>
    public string? WriteProblem { get; set; }

    /// <summary>When set, a read throws it, as a read does when the log folder can't be listed.</summary>
    public Exception? Failure { get; set; }

    /// <summary>How many reads started.</summary>
    public int ReadCount
    {
        get { lock (_gate) { return _readThreads.Count; } }
    }

    /// <summary>The managed id of the thread each read was called on, in order.</summary>
    public IReadOnlyList<int> ReadThreads
    {
        get { lock (_gate) { return [.. _readThreads]; } }
    }

    /// <summary>Keeps every read that starts from now on open until the returned action is called, which releases them all.</summary>
    public Action Hold()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) { _hold = hold; }
        return () => hold.TrySetResult();
    }

    /// <summary>A task that completes once this many reads have finished, whether they returned or threw.</summary>
    public Task WhenReadsFinished(int count)
    {
        lock (_gate)
        {
            if (_finished >= count)
            {
                return Task.CompletedTask;
            }

            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((count, done));
            return done.Task;
        }
    }

    /// <inheritdoc />
    public async Task<LogRead> ReadAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource? hold;
        LogRead found;
        Exception? failure;
        lock (_gate)
        {
            _readThreads.Add(Environment.CurrentManagedThreadId);
            hold = _hold;
            failure = Failure;
            found = new LogRead([.. Entries], UnreadableLines, WriteProblem);
        }

        try
        {
            if (hold is not null)
            {
                await hold.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return failure is null ? found : throw failure;
        }
        finally
        {
            List<TaskCompletionSource> ready;
            lock (_gate)
            {
                _finished++;
                ready = [.. _waiters.Where(w => w.Count <= _finished).Select(w => w.Done)];
                _waiters.RemoveAll(w => w.Count <= _finished);
            }

            ready.ForEach(done => done.TrySetResult());
        }
    }
}
