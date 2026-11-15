using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Removing the images an edit of a task leaves. They stay while the user can still Undo a discarded edit, and go when it
/// can't be undone any more. A known failure is reported and the caller goes on; it is not retried.
/// </summary>
internal sealed class EditCleanup(ITasks tasks, PendingUndo pending, Action<string> report)
{
    /// <summary>
    /// Makes ready a fresh edit of the task: finishes its own pending discard, then removes what an earlier edit left.
    /// False when that can't be done, so the edit must not open.
    /// </summary>
    public bool BeginFresh(string taskId)
    {
        pending.CommitIfFor(taskId);
        return Clean(taskId);
    }

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
            report("The edit's images couldn't be removed: " + reason);
            return false;
        }
    }
}
