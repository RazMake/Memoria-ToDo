namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Every task, and the operations on them. Safe to call from any thread; runs one call at a time.</summary>
public interface ITasks
{
    /// <summary>Reads the clone's tasks, then raises <see cref="Changed"/>. Null means no folder is set: the list is empty.</summary>
    void UseRepositoryFolder(string? folder);

    /// <summary>Every task, open and resolved, in rank order; tasks with the same rank in ID order.</summary>
    IReadOnlyList<TaskItem> All { get; }

    /// <summary>Raised on the calling thread after any change to <see cref="All"/>.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Adds the draft's task at the top, writes its file, moves its images into the clone, then deletes the kept draft.
    /// Throws <see cref="NoRepositoryFolderException"/> when no folder is set, and <see cref="ArgumentException"/>
    /// for an empty or multi-line title or an invalid tag.
    /// </summary>
    TaskItem Add(TaskDraft draft);

    /// <summary>The tags in use and how many tasks have each, most-used first, ties in alphabetical order.</summary>
    IReadOnlyList<TagUse> TagsInUse();

    /// <summary>
    /// <paramref name="tags"/> with the typed tag added, spelled as a matching tag in use; unchanged when already there.
    /// Throws <see cref="ArgumentException"/> for an empty or multi-line tag.
    /// </summary>
    IReadOnlyList<string> WithTag(IReadOnlyList<string> tags, string typed);

    /// <summary>Tags in use that start with <paramref name="typed"/>, ignoring case, except those in <paramref name="tags"/>, most-used first.</summary>
    IReadOnlyList<string> SuggestTags(string typed, IReadOnlyList<string> tags);

    /// <summary>Tasks whose title, description, or tags hold every typed word, ignoring case, in rank order.</summary>
    IReadOnlyList<TaskItem> Search(string text);

    /// <summary>The address a link to the task uses: <c>task:&lt;id&gt;</c>.</summary>
    string LinkAddress(string taskId);

    /// <summary>The task ID in <paramref name="address"/>, or null when it isn't a task link.</summary>
    string? TaskIdIn(string address);

    /// <summary>A new, empty draft with a new task ID. Keeps nothing.</summary>
    TaskDraft StartDraft();

    /// <summary>The kept draft, or null when there's none. Throws <see cref="InvalidDataException"/> when it can't be read.</summary>
    TaskDraft? LoadDraft();

    /// <summary>Replaces the kept draft with this one.</summary>
    void KeepDraft(TaskDraft draft);

    /// <summary>Deletes the kept draft and its images, and the images saved for any draft this run, including one never kept.</summary>
    void DiscardDraft();

    /// <summary>
    /// Shrinks the image to at most 1 MB, keeps it with the draft, and returns its Markdown, such as
    /// <c>![alt](images/&lt;task-id&gt;/&lt;image-id&gt;.png)</c>. Throws <see cref="InvalidDataException"/> when it
    /// isn't an image, and <see cref="ImageTooLargeException"/> when it can't be shrunk to 1 MB.
    /// </summary>
    string SaveDraftImage(string taskId, byte[] image, string altText);
}
