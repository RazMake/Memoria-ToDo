using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Changing the tags of a task from the list, with no Edit. Each change saves at once through Tasks' save of an edit, with the
/// task as shown and the tag control's whole new list, then waits on the Undo bar, which says what kind of change it was and
/// saves the whole list the task had, in its order. A failure the list knows is reported with its reason and nothing is saved,
/// except after several failures at once, when the change may be saved, as <see cref="UndoableSave"/> says, and after a notice
/// failed, when the change is saved.
/// Only for a task with no open edit, since during an edit the tags join the edit's one save. A change first finishes the Undo bar
/// images of a discarded edit, so its save finds none waiting. Starting an edit of the task finishes the task's own bar, keeping
/// its change, since its Undo would save the old tags over the edit.
/// </summary>
internal sealed class TagChanges(ITasks tasks, PendingUndo pending, EditCleanup cleanup, Action<string> report, ILogger logger)
{
    private const string AddedLabel = "Tag added";
    private const string RemovedLabel = "Tag removed";
    private const string ChangedLabel = "Tag changed";
    private const string ReorderedLabel = "Tags reordered";

    private static readonly string[] Labels = [AddedLabel, RemovedLabel, ChangedLabel, ReorderedLabel];

    private readonly UndoableSave _saves = new(tasks, pending, cleanup, report, logger);

    /// <summary>Saves the task with <paramref name="tags"/> as its tags, replacing what it had. Returns the saved task, or null when it couldn't be saved.</summary>
    public TaskItem? Change(TaskItem shown, IReadOnlyList<string> tags)
    {
        var before = shown.Tags;
        return _saves.Run(shown, LabelOf(before, tags), task => WithTags(task, tags), saved => WithTags(saved, before));
    }

    /// <summary>Finishes the Undo bar that shows when it is this task's tag change, keeping the change; any other bar stays.</summary>
    public void FinishFor(string taskId)
    {
        if (pending.TaskId == taskId && pending.Label is { } label && Labels.Contains(label))
        {
            pending.Commit();
        }
    }

    // Added or removed when the other list holds all of this one's tags, reordered when both hold the same tags, else changed.
    private static string LabelOf(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var was = before.Select(TagName.Normalize).ToHashSet();
        var now = after.Select(TagName.Normalize).ToHashSet();
        if (was.SetEquals(now))
        {
            return ReorderedLabel;
        }

        return was.IsSubsetOf(now) ? AddedLabel : now.IsSubsetOf(was) ? RemovedLabel : ChangedLabel;
    }

    private static TaskDraft WithTags(TaskItem task, IReadOnlyList<string> tags) => new(task.Id, task.Title, task.Description, tags);
}
