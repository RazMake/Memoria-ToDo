namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// Task changes for a caller with no editor: the Local API and the MCP server. Never reads or changes the kept draft or an open
/// edit's waiting images. Safe to call from any thread; uses the task store's lock. Each change writes the task's file,
/// then raises Changed on the calling thread and reports the local save, as ITasks' changes do. Each change throws
/// NoticeFailedException of TaskItem when it was saved and then a notice handler or the local save report failed, even for one
/// failure; its Saved is the task the call returns.
/// </summary>
public interface IDirectTaskChanges
{
    /// <summary>
    /// Adds an open task with a new ID, at the top, with Created and Updated set to now and the tags normalized, each repeat dropped. Returns it.
    /// Throws, before writing: ArgumentException for an empty or multi-line title, a tag that's empty once normalized, or more than 15 tags;
    /// NoRepositoryFolderException when no folder is set.
    /// </summary>
    TaskItem Add(string title, string description, IReadOnlyList<string> tags);

    /// <summary>
    /// Replaces each field that isn't null; an empty tag list removes every tag. The tags are normalized, each repeat dropped.
    /// Sets Updated, unless nothing changed, which writes nothing, as when the list equals the current one in order. Returns the task as saved. Throws, before writing: StaleTaskException when the task's Updated isn't
    /// baseUpdated; TaskNotFoundException; ArgumentException for an empty or multi-line title, a tag that's empty once normalized, or more than 15 tags;
    /// NoRepositoryFolderException.
    /// </summary>
    TaskItem Update(string taskId, DateTimeOffset baseUpdated, string? title, string? description, IReadOnlyList<string>? tags);

    /// <summary>
    /// Shrinks the image to at most 1 MB, writes it into the task's image folder in the clone, and appends its Markdown on
    /// a new line at the end of the description, in one save that sets Updated. Deletes the image again when the save
    /// fails. Returns the task as saved. Throws, before writing: TaskNotFoundException; InvalidDataException when it
    /// isn't an image; ImageTooLargeException; NoRepositoryFolderException.
    /// </summary>
    TaskItem AddImage(string taskId, byte[] image, string altText);
}
