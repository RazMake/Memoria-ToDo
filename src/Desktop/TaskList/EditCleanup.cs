using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Removing the images an edit of a task leaves. They stay while the user can still Undo a discarded edit, and go when it
/// can't be undone any more. A known failure is logged and reported and the caller goes on; it is not retried.
/// </summary>
internal sealed class EditCleanup(ITasks tasks, PendingUndo pending, Action<string> report, ILogger logger)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    /// <summary>
    /// Makes ready a fresh edit of the task: finishes its own pending discard, then removes what an earlier edit left.
    /// False when that can't be done, so the edit must not open.
    /// </summary>
    public bool BeginFresh(string taskId)
    {
        FinishDiscard(taskId);
        return Clean(taskId);
    }

    /// <summary>Finishes the task's own pending discard, if any, which removes the images that edit left. Never removes the images of an edit still open.</summary>
    public void FinishDiscard(string taskId) => pending.CommitIfFor(taskId);

    /// <summary>Finishes the Undo bar that shows, about any task, keeping its change; a discarded edit's bar removes the images that edit left.</summary>
    public void FinishBar() => pending.Commit();

    /// <summary>
    /// Closes the editor without saving. A clean one throws its images away now; a changed one can be brought back
    /// with Undo through <paramref name="restore"/>, and throws them away when that can't happen any more.
    /// </summary>
    public void Leave(string taskId, bool hasChanges, Action restore)
    {
        if (hasChanges)
        {
            pending.Start("Edit discarded", taskId, () => Clean(taskId), restore);
        }
        else
        {
            Clean(taskId);
        }
    }

    /// <summary>Throws the images of an edit still open away, as the window closes.</summary>
    public void Close(string taskId) => Clean(taskId);

    private bool Clean(string taskId)
    {
        try
        {
            tasks.DiscardEditImages(taskId);
            return true;
        }
        catch (Exception failure) when (ListFailures.MessageOf(failure) is { } reason)
        {
            _logger.Error(failure, "An edit's images couldn't be removed: {Reason}", reason);
            report("The edit's images couldn't be removed: " + reason);
            return false;
        }
    }
}
