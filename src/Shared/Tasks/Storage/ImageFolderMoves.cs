using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Moving the images of a draft or an open edit into the clone's folder for the task.</summary>
internal static class ImageFolderMoves
{
    /// <summary>Where each image in <paramref name="from"/> goes in <paramref name="to"/>: its path now, and its path there.</summary>
    public static IReadOnlyList<(string From, string To)> Of(IFileSystem files, string from, string to) =>
        [.. files.ListFiles(from).Select(path => (path, Path.Combine(to, Path.GetFileName(path))))];
}
