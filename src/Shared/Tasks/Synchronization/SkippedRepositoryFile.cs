namespace Memoria.ToDo.Shared.Tasks;

/// <summary>A repository file Tasks skipped while loading, and why.</summary>
internal sealed record SkippedRepositoryFile(string RelativePath, string Reason, bool RequiresUpdate) : ISkippedRepositoryFile
{
    /// <summary>The skipped file at <paramref name="relativePath"/>; it needs a newer app when that's why it couldn't be read.</summary>
    public static SkippedRepositoryFile Of(string relativePath, Exception failure) =>
        new(relativePath, failure.Message, failure.InnerException is NewerVersionException);
}
