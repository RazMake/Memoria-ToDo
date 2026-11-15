using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// Adding or removing one tag of a task from the list, with no Edit. Each change saves at once through Tasks' save of an
/// edit, with the task as shown and only its tags different, then waits on the Undo bar, which saves the opposite change.
/// A failure the list knows is reported with its reason and nothing is saved.
/// </summary>
internal sealed class TagChanges(ITasks tasks, PendingUndo pending, EditCleanup cleanup, Action<string> report)
{
    private readonly UndoableSave _saves = new(tasks, pending, cleanup, report);

    /// <summary>Saves the task with the tag added at the end. Returns the saved task, or null when it couldn't be saved.</summary>
    public TaskItem? Add(TaskItem shown, string tag) =>
        _saves.Run(shown, "Tag added", task => WithTags(task, tasks.WithTag(task.Tags, tag)), saved => WithTags(saved, Without(saved.Tags, tag)));

    /// <summary>Saves the task without the tag, ignoring case. Returns the saved task, or null when it couldn't be saved.</summary>
    public TaskItem? Remove(TaskItem shown, string tag) =>
        _saves.Run(shown, "Tag removed", task => WithTags(task, Without(task.Tags, tag)), saved => WithTags(saved, tasks.WithTag(saved.Tags, tag)));

    private static TaskDraft WithTags(TaskItem task, IReadOnlyList<string> tags) => new(task.Id, task.Title, task.Description, tags);

    private static IReadOnlyList<string> Without(IReadOnlyList<string> tags, string tag) =>
        [.. tags.Where(existing => !string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase))];
}
