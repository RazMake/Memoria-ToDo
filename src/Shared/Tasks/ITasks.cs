using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Every task, and the operations on them. Safe to call from any thread; runs one call at a time.</summary>
public interface ITasks
{
    /// <summary>Every task, open and resolved, in rank order; tasks with the same rank in ID order.</summary>
    IReadOnlyList<TaskItem> All { get; }

    /// <summary>Raised on the calling thread after any change to <see cref="All"/>.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Adds the draft's task at the top, writes its file, moves its images into the clone, then deletes the kept draft.
    /// Throws <see cref="NoRepositoryFolderException"/> when no folder is set, and <see cref="ArgumentException"/>
    /// for an empty or multi-line title, an invalid tag, or two or more tags, before any file is written.
    /// </summary>
    TaskItem Add(TaskDraft draft);

    /// <summary>The tags in use and how many tasks have each, most-used first, ties in alphabetical order.</summary>
    IReadOnlyList<TagUse> TagsInUse();

    /// <summary>
    /// <paramref name="tags"/> with the typed tag added, spelled as a matching tag in use; unchanged when already there.
    /// Throws <see cref="ArgumentException"/> for an empty or multi-line tag.
    /// </summary>
    IReadOnlyList<string> WithTag(IReadOnlyList<string> tags, string typed);

    /// <summary>Tags in use that contain <paramref name="typed"/> anywhere, ignoring case, except those in <paramref name="tags"/>, most-used first.</summary>
    IReadOnlyList<string> SuggestTags(string typed, IReadOnlyList<string> tags);

    /// <summary>Tasks whose title, description, or tags hold every typed word, ignoring case, in rank order.</summary>
    IReadOnlyList<TaskItem> Search(string text);

    /// <summary><see cref="Search(string)"/>, narrowed to the tasks that have <paramref name="tag"/>, ignoring case.</summary>
    IReadOnlyList<TaskItem> Search(string text, string tag);

    /// <summary>The address a link to the task uses: <c>task:&lt;id&gt;</c>.</summary>
    string LinkAddress(string taskId);

    /// <summary>The task ID in <paramref name="address"/>, or null when it isn't a task link.</summary>
    string? TaskIdIn(string address);

    /// <summary>A new, empty draft with a new task ID. Keeps nothing.</summary>
    TaskDraft StartDraft();

    /// <summary>The kept draft, or null when there's none, with its first tag only. Throws <see cref="InvalidDataException"/> when it can't be read.</summary>
    TaskDraft? LoadDraft();

    /// <summary>Replaces the kept draft with this one.</summary>
    void KeepDraft(TaskDraft draft);

    /// <summary>Deletes the kept draft and its images, and the images saved for any draft this run, including one never kept.</summary>
    void DiscardDraft();

    /// <summary>
    /// Shrinks the image to at most 1 MB and returns its Markdown, such as
    /// <c>![alt](images/&lt;task-id&gt;/&lt;image-id&gt;.png)</c>. Keeps it with the open edit when <paramref name="taskId"/>
    /// is a task that already exists, and with the draft otherwise. Throws <see cref="InvalidDataException"/> when it
    /// isn't an image, and <see cref="ImageTooLargeException"/> when it can't be shrunk to 1 MB.
    /// </summary>
    string SaveImage(string taskId, byte[] image, string altText);

    /// <summary>Deletes the images kept for the task's open edit. Never touches the draft.</summary>
    void DiscardEditImages(string taskId);

    /// <summary>
    /// Saves an edit of <paramref name="started"/>, the task as the editor got it, onto the task as it is now. A Title or
    /// Description that differs from started's replaces the current one; a tag that differs from started's, ignoring case, replaces
    /// every current tag, where no tag counts as a value, and the started task's first tag is its tag. Every other field keeps its
    /// current value. Moves the edit's waiting images into the clone, on any save that changes something, then writes the file and sets Updated,
    /// unless nothing changed. Returns the task as saved.
    /// Throws, before any change: <see cref="NoRepositoryFolderException"/> when no folder is set;
    /// <see cref="TaskNotFoundException"/> when the task isn't in <see cref="All"/>; <see cref="ArgumentException"/> when
    /// <c>edited.TaskId</c> isn't <c>started.Id</c>, or for an empty or multi-line title, an invalid tag, or two or more tags.
    /// </summary>
    TaskItem SaveEdit(TaskItem started, TaskDraft edited);

    /// <summary>
    /// Gives the task a rank that shows it after <paramref name="previousTaskId"/> and before <paramref name="nextTaskId"/>; null
    /// leaves that side open. The new rank is never one another task holds: with a previous task, it sorts after that task's rank
    /// and before the first rank after it in <see cref="All"/> that differs from it, which is never past the next task; with only a
    /// next task, before that task's rank and after the last rank before it in <see cref="All"/> that differs from it. Both skip the
    /// moved task. Changes only the task's own file and sets Updated. Changes nothing when the task already shows there: between the
    /// two given tasks, or, with a side open, next to the given task, with only tasks sharing its rank between them. Returns the task
    /// as saved. Throws <see cref="NoRepositoryFolderException"/> when no folder is set,
    /// <see cref="TaskNotFoundException"/> when any ID isn't in <see cref="All"/>, and
    /// <see cref="ArgumentException"/> when a neighbor is the task itself, the previous task doesn't show before the next, or the
    /// neighbors share a rank, so no rank of the task's own shows it between them.
    /// </summary>
    TaskItem Move(string taskId, string? previousTaskId, string? nextTaskId);

    /// <summary>Sets Resolved to now and advances Updated. A resolved task stays as it is. Returns the task as saved. Throws <see cref="NoRepositoryFolderException"/> when no folder is set, and <see cref="TaskNotFoundException"/> when the task isn't in <see cref="All"/>.</summary>
    TaskItem Resolve(string taskId);

    /// <summary>Clears Resolved and advances Updated. An open task stays as it is. Returns the task as saved. Throws <see cref="NoRepositoryFolderException"/> when no folder is set, and <see cref="TaskNotFoundException"/> when the task isn't in <see cref="All"/>.</summary>
    TaskItem Reopen(string taskId);

    /// <summary>
    /// Deletes the task's file, then its images in the clone, then any images kept for its open edit. Throws
        /// <see cref="NoRepositoryFolderException"/> when no folder is set, and <see cref="TaskNotFoundException"/> when the task isn't in <see cref="All"/>. Stops at the first step that fails and
    /// throws its error; once the file is deleted, the task leaves <see cref="All"/> and <see cref="Changed"/> is raised even if a later step fails.
    /// </summary>
    void Delete(string taskId);
}
