using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The one thing the Undo bar can undo at a time. It is finished for good when its time is up, when another one
/// replaces it, or when <see cref="Commit"/> is called, for example as the window closes.
/// </summary>
internal sealed class PendingUndo(TimeProvider time, Dispatcher dispatcher, Action changed) : IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(10);

    private Action? _commit;
    private Action? _undo;
    private string? _taskId;
    private ITimer? _timer;
    private int _generation;

    /// <summary>Whether something can be undone now, so the bar shows.</summary>
    public bool IsPending => _commit is not null;

    /// <summary>What the bar says is undoable.</summary>
    public string Label { get; private set; } = "";

    /// <summary>
    /// Finishes the pending one, then makes this one, about the task <paramref name="taskId"/>, the pending one.
    /// <paramref name="commit"/> finishes it for good; <paramref name="undo"/> brings it back.
    /// </summary>
    public void Start(string label, string taskId, Action commit, Action undo)
    {
        Commit();
        Label = label;
        _taskId = taskId;
        _commit = commit;
        _undo = undo;
        var generation = ++_generation;
        _timer = time.CreateTimer(
            _ => dispatcher.BeginInvoke(() =>
            {
                if (generation == _generation)
                {
                    Commit();
                }
            }),
            null,
            Lifetime,
            Timeout.InfiniteTimeSpan);
        changed();
    }

    /// <summary>Brings the pending one back. Does nothing when none is pending.</summary>
    public void Undo()
    {
        var undo = Clear()?.undo;
        changed();
        undo?.Invoke();
    }

    /// <summary>Finishes the pending one for good when it is about <paramref name="taskId"/>; leaves any other one pending.</summary>
    public void CommitIfFor(string taskId)
    {
        if (_taskId == taskId)
        {
            Commit();
        }
    }

    /// <summary>Finishes the pending one for good. Does nothing when none is pending.</summary>
    public void Commit()
    {
        var commit = Clear()?.commit;
        changed();
        commit?.Invoke();
    }

    public void Dispose() => Clear();

    private (Action commit, Action undo)? Clear()
    {
        _timer?.Dispose();
        _timer = null;
        _generation++;
        if (_commit is null || _undo is null)
        {
            return null;
        }

        var pending = (_commit, _undo);
        _taskId = null;
        _commit = null;
        _undo = null;
        return pending;
    }
}
