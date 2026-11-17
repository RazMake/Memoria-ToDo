using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Choosing or clearing the one tag of a task from the list, with no Edit. Each change saves at once through Tasks' save of an
/// edit, with the task as shown and only its tag different, then waits on the Undo bar, which saves the tag the pill showed
/// before: the first of the task's tags, or none. A failure the list knows is reported with its reason and nothing is saved.
/// Only for a task with no open edit, since during an edit the tag joins the edit's one save. A change first finishes the Undo bar
/// images of a discarded edit, so its save finds none waiting. Starting an edit of the task finishes the task's own bar, keeping
/// its change, since its Undo would save the old tag over the edit.
/// </summary>
internal sealed class TagChanges(ITasks tasks, PendingUndo pending, EditCleanup cleanup, Action<string> report)
{
    private const string ChangedLabel = "Tag changed";
    private const string ClearedLabel = "Tag cleared";

    private readonly UndoableSave _saves = new(tasks, pending, cleanup, report);

    /// <summary>Saves the task with the tag as its one tag, replacing what it had. Returns the saved task, or null when it couldn't be saved.</summary>
    public TaskItem? Choose(TaskItem shown, string tag) => Change(shown, ChangedLabel, [tag]);

    /// <summary>Saves the task with no tag. Returns the saved task, or null when it couldn't be saved.</summary>
    public TaskItem? Clear(TaskItem shown) => Change(shown, ClearedLabel, []);

    /// <summary>Finishes the Undo bar that shows when it is this task's tag change, keeping the change; any other bar stays.</summary>
    public void FinishFor(string taskId)
    {
        if (pending.TaskId == taskId && pending.Label is ChangedLabel or ClearedLabel)
        {
            pending.Commit();
        }
    }

    private TaskItem? Change(TaskItem shown, string label, IReadOnlyList<string> tags)
    {
        IReadOnlyList<string> before = [.. shown.Tags.Take(1)];
        return _saves.Run(shown, label, task => WithTags(task, tags), saved => WithTags(saved, before));
    }

    private static TaskDraft WithTags(TaskItem task, IReadOnlyList<string> tags) => new(task.Id, task.Title, task.Description, tags);
}
