using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// A change of one task made from the list with no Edit: it saves at once through Tasks' save of an edit, with the task as
/// shown, then waits on the Undo bar, which saves the opposite change. A failure the list knows is reported with its reason
/// and nothing is saved.
/// </summary>
internal sealed class UndoableSave(ITasks tasks, PendingUndo pending, EditCleanup cleanup, Action<string> report)
{
    /// <summary>
    /// Saves <paramref name="shown"/> as <paramref name="change"/> makes it, and puts <paramref name="label"/> on the Undo bar,
    /// where Undo saves what <paramref name="opposite"/> makes of the saved task. Returns the saved task, or null when it
    /// couldn't be saved.
    /// </summary>
    public TaskItem? Run(TaskItem shown, string label, Func<TaskItem, TaskDraft> change, Func<TaskItem, TaskDraft> opposite)
    {
        // Saving would move the images an earlier edit left into the clone, so they go first.
        if (!cleanup.BeginFresh(shown.Id) || !TrySave(shown, change, out var saved))
        {
            return null;
        }

        pending.Start(label, saved.Id, () => { }, () => TrySave(saved, opposite, out _));
        return saved;
    }

    private bool TrySave(TaskItem started, Func<TaskItem, TaskDraft> draft, out TaskItem saved)
    {
        try
        {
            saved = tasks.SaveEdit(started, draft(started));
            return true;
        }
        catch (Exception failure) when (ListFailures.MessageOf(failure) is { } reason)
        {
            report(reason);
            saved = started;
            return false;
        }
    }
}
