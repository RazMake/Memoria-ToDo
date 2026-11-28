namespace Memoria.ToDo.Shared.Platform;

/// <summary>How a store skips a file it can't read: it leaves the file unchanged and reports it.</summary>
public static class SkippedFile
{
    /// <summary>Whether a failure to read or parse a file that's there means the store skips the file.</summary>
    public static bool Skips(Exception failure) =>
        failure is InvalidDataException or IOException or UnauthorizedAccessException;

    /// <summary>
    /// The report for the skipped file at <paramref name="relativePath"/>: the failure's message as the reason, and
    /// <see cref="ISkippedRepositoryFile.RequiresUpdate"/> true when <see cref="JsonFile.RequireVersion"/> found a version above the newest.
    /// </summary>
    public static ISkippedRepositoryFile Of(string relativePath, Exception failure) =>
        new Report(relativePath, failure.Message, failure.InnerException is NewerVersionException);

    private sealed record Report(string RelativePath, string Reason, bool RequiresUpdate) : ISkippedRepositoryFile;
}
