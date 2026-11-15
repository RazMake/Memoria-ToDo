using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Adding or removing one tag of a task from the list, with no Edit. Each change saves at once through Tasks' save of an
/// edit, with the task as shown and only its tags different, then waits on the Undo bar, which saves the opposite change.
/// A failure the list knows is reported with its reason and nothing is saved.
/// </summary>
internal sealed class TagChanges(ITasks tasks, PendingUndo pending, EditCleanup cleanup, Action<string> report)
{
    /// <summary>Saves the task with the tag added at the end. Returns the saved task, or null when it couldn't be saved.</summary>
    public TaskItem? Add(TaskItem shown, string tag) =>
        Change(shown, "Tag added", tasks.WithTag(shown.Tags, tag), saved => Without(saved.Tags, tag));

    /// <summary>Saves the task without the tag, ignoring case. Returns the saved task, or null when it couldn't be saved.</summary>
    public TaskItem? Remove(TaskItem shown, string tag) =>
        Change(shown, "Tag removed", Without(shown.Tags, tag), saved => tasks.WithTag(saved.Tags, tag));

    private TaskItem? Change(TaskItem shown, string label, IReadOnlyList<string> tags, Func<TaskItem, IReadOnlyList<string>> opposite)
    {
        // Saving would move the images an earlier edit left into the clone, so they go first.
        if (!cleanup.BeginFresh(shown.Id) || !TrySave(shown, tags, out var saved))
        {
            return null;
        }

        pending.Start(label, saved.Id, () => { }, () => TrySave(saved, opposite(saved), out _));
        return saved;
    }

    private bool TrySave(TaskItem started, IReadOnlyList<string> tags, out TaskItem saved)
    {
        try
        {
            saved = tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, started.Description, tags));
            return true;
        }
        catch (Exception failure) when (ListFailures.MessageOf(failure) is { } reason)
        {
            report(reason);
            saved = started;
            return false;
        }
    }

    private static IReadOnlyList<string> Without(IReadOnlyList<string> tags, string tag) =>
        [.. tags.Where(existing => !string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase))];
}
