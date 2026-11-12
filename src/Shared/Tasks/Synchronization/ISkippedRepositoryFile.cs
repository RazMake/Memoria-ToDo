namespace Memoria.ToDo.Shared.Tasks;

/// <summary>An immutable diagnostic for a repository file that could not be loaded.</summary>
public interface ISkippedRepositoryFile
{
    /// <summary>The file's path relative to the clone.</summary>
    string RelativePath { get; }

    /// <summary>The reason, suitable for showing in sync status.</summary>
    string Reason { get; }

    /// <summary>Whether reading the file requires a newer application version.</summary>
    bool RequiresUpdate { get; }
}
