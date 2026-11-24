namespace Memoria.ToDo.Shared.Platform;

/// <summary>
/// A call needs a store file that was skipped at the latest load, so it changed nothing.
/// The message names the file's relative path and the reason it was skipped.
/// </summary>
public sealed class SkippedFileException : InvalidOperationException
{
    public SkippedFileException(ISkippedRepositoryFile file) : base(MessageOf(file)) => File = file;

    private static string MessageOf(ISkippedRepositoryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return $"{file.RelativePath} was skipped: {file.Reason}";
    }

    /// <summary>The skipped file, as the store's skipped files list it.</summary>
    public ISkippedRepositoryFile File { get; }
}
