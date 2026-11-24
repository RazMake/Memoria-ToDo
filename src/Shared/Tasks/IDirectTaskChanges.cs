namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// Task changes for a caller with no editor: the Local API and the MCP server. Never reads or changes the kept draft or an open
/// edit's waiting images. Safe to call from any thread; uses the task store's lock. Each change writes the task's file,
/// then raises Changed on the calling thread and reports the local save, as ITasks' changes do.
/// </summary>
public interface IDirectTaskChanges
{
    /// <summary>
    /// Adds an open task with a new ID, at the top, with Created and Updated set to now; a null tag means none. Returns it.
    /// Throws, before writing: ArgumentException for an empty or multi-line title, or an invalid tag;
    /// NoRepositoryFolderException when no folder is set.
    /// </summary>
    TaskItem Add(string title, string description, string? tag);

    /// <summary>
    /// Replaces each field that isn't null; an empty tag removes every tag. Sets Updated, unless nothing changed, which
    /// writes nothing. Returns the task as saved. Throws, before writing: StaleTaskException when the task's Updated isn't
    /// baseUpdated; TaskNotFoundException; ArgumentException for an empty or multi-line title or an invalid tag;
    /// NoRepositoryFolderException.
    /// </summary>
    TaskItem Update(string taskId, DateTimeOffset baseUpdated, string? title, string? description, string? tag);

    /// <summary>
    /// Shrinks the image to at most 1 MB, writes it into the task's image folder in the clone, and appends its Markdown on
    /// a new line at the end of the description, in one save that sets Updated. Deletes the image again when the save
    /// fails. Returns the task as saved. Throws, before writing: TaskNotFoundException; InvalidDataException when it
    /// isn't an image; ImageTooLargeException; NoRepositoryFolderException.
    /// </summary>
    TaskItem AddImage(string taskId, byte[] image, string altText);
}
