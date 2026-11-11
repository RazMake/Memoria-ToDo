using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>This device's images for open edits of existing tasks, kept outside the clone and apart from the draft's images.</summary>
internal sealed class EditImageStore(IFileSystem files, string deviceFolder)
{
    /// <summary>Stores an image under the task's edit images.</summary>
    public void Save(string taskId, string fileName, byte[] image) =>
        files.WriteFile(Path.Combine(RepositoryLayout.EditImagesFolder(deviceFolder, taskId), fileName), image);

    /// <summary>Where each of the task's edit images goes in the clone: its path now, and its path there.</summary>
    public IReadOnlyList<(string From, string To)> ImageMoves(string taskId, string clone) =>
        ImageFolderMoves.Of(files, RepositoryLayout.EditImagesFolder(deviceFolder, taskId), RepositoryLayout.ImagesFolder(clone, taskId));

    /// <summary>Deletes the task's edit images, and nothing else.</summary>
    public void Discard(string taskId) => files.DeleteFolder(RepositoryLayout.EditImagesFolder(deviceFolder, taskId));
}
