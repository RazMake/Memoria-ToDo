using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// Reloads the selected clone half a second after the last change to a file in it, outside .git. Watch and Dispose may run on any thread;
/// the watch reports on thread-pool threads and the timer calls back on another. One lock guards the state and is never held
/// while calling the repository or stopping a watch.
/// </summary>
internal sealed class CloneReloads : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(500);

    private const string ChangesReason = "files changed";
    private const string OverflowReason = "the watcher's buffer overflowed";
    private const string FailureReason = "the watch failed";

    private readonly IFolderWatcher _watcher;
    private readonly ISyncedRepository _repository;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private readonly object _gate = new();

    private string? _folder;
    private Session? _session;
    private IDisposable? _watch;
    private bool _disposed;
    private string? _reason;        // why a reload is waiting; null when none is
    private long _restarted;        // when the wait last restarted
    private int _changes;           // the changes seen since the last reload
    private string? _firstPath;     // the first of them

    public CloneReloads(IFolderWatcher watcher, ISyncedRepository repository, TimeProvider time, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(watcher);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _watcher = watcher;
        _repository = repository;
        _logger = logger;
        _time = time;
        _timer = time.CreateTimer(_ => Reload(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Watches <paramref name="folder"/>, then selects it through <paramref name="select"/>, so a change made while the stores read it is seen.
    /// When the selection throws, the watch follows the folder the repository holds, and the exception is rethrown.
    /// </summary>
    public void Select(string? folder, Action<string?> select)
    {
        ArgumentNullException.ThrowIfNull(select);
        Watch(folder);
        try
        {
            select(folder);
        }
        catch
        {
            var held = _repository.Folder;
            if (!string.Equals(held, folder, StringComparison.Ordinal))
                Watch(held);
            throw;
        }
    }

    /// <summary>Moves the watch to <paramref name="folder"/>, or stops it for null, and cancels a reload waiting for the old folder. A watch that can't start is logged, not thrown.</summary>
    public void Watch(string? folder)
    {
        if (folder is not null && string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Must be a folder path, or null.", nameof(folder));

        Session? session = null;
        IDisposable? old;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            old = Stop();
            _folder = folder;
            if (folder is not null)
                _session = session = new Session(this);
        }

        old?.Dispose();
        if (session is null)
        {
            _logger.Information("Stopped watching the clone for changes");
            return;
        }

        IDisposable started;
        try
        {
            started = _watcher.Watch(folder!, session);
        }
        catch (Exception e)
        {
            lock (_gate)
            {
                if (_session == session)
                    _session = null;
            }

            _logger.Warning(e, "Can't watch the clone {Folder} for changes, so hand edits wait for the next sync", folder);
            return;
        }

        bool kept;
        lock (_gate)
            kept = _session == session && (_watch = started) is not null;
        if (kept)
            _logger.Information("Watching the clone {Folder} for changes", folder);
        else
            started.Dispose();
    }

    public void Dispose()
    {
        IDisposable? old;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            old = Stop();
            _folder = null;
        }

        old?.Dispose();
        _timer.Dispose();
    }

    /// <summary>Ignores the current watch's reports, cancels a waiting reload, and returns the watch to dispose. Call under the lock.</summary>
    private IDisposable? Stop()
    {
        var old = _watch;
        _watch = null;
        _session = null;
        _reason = null;
        _changes = 0;
        _firstPath = null;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        return old;
    }

    /// <summary>Restarts the wait for <paramref name="reason"/>. Call under the lock, for the current session.</summary>
    private void Schedule(string reason)
    {
        _reason = reason;
        _restarted = _time.GetTimestamp();
        _timer.Change(Wait, Timeout.InfiniteTimeSpan);
    }

    private void Reload()
    {
        string folder, reason;
        int changes;
        string? firstPath;
        lock (_gate)
        {
            if (_reason is null || _folder is null)
                return;
            // A callback queued before the last restart can't be taken back; wait out the rest.
            var left = Wait - _time.GetElapsedTime(_restarted);
            if (left > TimeSpan.Zero)
            {
                _timer.Change(left, Timeout.InfiniteTimeSpan);
                return;
            }

            (folder, reason, changes, firstPath) = (_folder, _reason, _changes, _firstPath);
            _reason = null;
            _changes = 0;
            _firstPath = null;
        }

        _logger.Debug("Reloading the clone {Folder}: {Reason}; {Changes} changes seen, the first {FirstPath}", folder, reason, changes, firstPath);
        try
        {
            _repository.Update(folder, () => { });
        }
        catch (InvalidOperationException e) when (!string.Equals(_repository.Folder, folder, StringComparison.Ordinal))
        {
            _logger.Debug(e, "Skipped reloading the clone {Folder}, since it is no longer the selected folder: {Reason}", folder, reason);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Reloading the clone {Folder} after a change on disk failed", folder);
        }
    }

    /// <summary>One watch's reports. Once the watch is replaced, stopped, or failed, they're ignored.</summary>
    private sealed class Session(CloneReloads owner) : IFolderChanges
    {
        public void Changed(string relativePath)
        {
            if (IsInGitFolder(relativePath))
                return;
            lock (owner._gate)
            {
                if (owner._session != this)
                    return;
                if (owner._changes++ == 0)
                    owner._firstPath = relativePath;
                owner.Schedule(owner._reason == OverflowReason ? OverflowReason : ChangesReason);
            }
        }

        public void Overflowed()
        {
            lock (owner._gate)
            {
                if (owner._session == this)
                    owner.Schedule(OverflowReason);
            }
        }

        public void Failed(Exception error)
        {
            string? folder;
            IDisposable? watch;
            lock (owner._gate)
            {
                if (owner._session != this)
                    return;
                folder = owner._folder;
                watch = owner._watch;
                owner._watch = null;
                owner._session = null;
                owner.Schedule(FailureReason);
            }

            owner._logger.Warning(error, "Watching the clone {Folder} failed, so it reloads once and hand edits wait for the next sync", folder);
            watch?.Dispose();
        }

        private static bool IsInGitFolder(string path)
        {
            var span = path.AsSpan();
            var end = span.IndexOfAny('\\', '/');
            return (end < 0 ? span : span[..end]).Equals(".git", StringComparison.OrdinalIgnoreCase);
        }
    }
}
