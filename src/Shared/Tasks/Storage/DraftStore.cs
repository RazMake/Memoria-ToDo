using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>This device's kept draft and its images, in the device folder.</summary>
internal sealed class DraftStore(IFileSystem files, string deviceFolder)
{
    private readonly HashSet<string> imageIds = [];

    private string DraftFile => RepositoryLayout.DraftFile(deviceFolder);

    /// <summary>The kept draft, or null when there's none. Throws <see cref="InvalidDataException"/> when it can't be read, keeping the system's exception as the cause.</summary>
    public TaskDraft? Load()
    {
        if (!files.FileExists(DraftFile))
            return null;
        byte[] content;
        try
        {
            content = files.ReadFile(DraftFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The kept draft can't be read.", e);
        }

        return DraftFileFormat.Read(content);
    }

    /// <summary>Replaces the kept draft. A blank draft is discarded instead, and a draft with a new ID drops the old one's images.</summary>
    public void Keep(TaskDraft draft)
    {
        TaskId.ThrowIfInvalid(draft.TaskId, nameof(draft));
        if (IsBlank(draft))
        {
            Discard();
            return;
        }

        var previous = KeptId();
        files.WriteFile(DraftFile, DraftFileFormat.Write(draft));
        if (previous is not null && previous != draft.TaskId)
            files.DeleteFolder(RepositoryLayout.ImagesFolder(deviceFolder, previous));
    }

    /// <summary>
    /// Deletes the kept draft and the images of the kept draft and of every task ID that got an image since the app started, such as a
    /// draft never kept. Images left by an earlier run for a draft that was never kept stay, because the file system can't list folders.
    /// The images go first, so a failure leaves the draft whole rather than without its images.
    /// </summary>
    public void Discard()
    {
        var ids = new HashSet<string>(imageIds);
        if (KeptId() is { } kept)
            ids.Add(kept);
        foreach (var id in ids)
            files.DeleteFolder(RepositoryLayout.ImagesFolder(deviceFolder, id));
        files.DeleteFile(DraftFile);
        imageIds.Clear();
    }

    /// <summary>Stores an image under the task's draft images and returns its file name.</summary>
    public void SaveImage(string taskId, string fileName, byte[] image)
    {
        files.WriteFile(Path.Combine(RepositoryLayout.ImagesFolder(deviceFolder, taskId), fileName), image);
        imageIds.Add(taskId);
    }

    /// <summary>Where each of the task's draft images goes in the clone: its path now, and its path there.</summary>
    public IReadOnlyList<(string From, string To)> ImageMoves(string taskId, string clone)
    {
        var target = RepositoryLayout.ImagesFolder(clone, taskId);
        return [.. files.ListFiles(RepositoryLayout.ImagesFolder(deviceFolder, taskId))
            .Select(path => (path, Path.Combine(target, Path.GetFileName(path))))];
    }

    private string? KeptId()
    {
        try
        {
            return Load()?.TaskId;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static bool IsBlank(TaskDraft draft) =>
        string.IsNullOrWhiteSpace(draft.Title) && string.IsNullOrWhiteSpace(draft.Description)
        && draft.Tags.All(string.IsNullOrWhiteSpace);
}
