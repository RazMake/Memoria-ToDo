namespace Memoria.ToDo.Shared.Platform;

/// <summary>One store's files in the clone, as the repository loads them.</summary>
public interface IRepositoryPart
{
    /// <summary>
    /// Reads the folder's files, or nothing for null, without using them, and returns the call that
    /// makes what it read current, or empties the part for null. Both run under the lock and raise
    /// no notice. A failed read     leaves the part as it was. The returned call never throws.    /// </summary>
    Action Load(string? folder);

    /// <summary>Raises the part's change notices. Runs after the lock is released.</summary>
    void Loaded();

    /// <summary>The files the latest Load skipped.</summary>
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }
}
