namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Sync's access to Tasks, without Git or network operations.</summary>
public interface ITaskSynchronization
{
    /// <summary>The shared layout and pure merge rules; these never read or write files.</summary>
    IRepositoryFileRules Files { get; }

    /// <summary>
    /// Creates missing settings with untouched defaults. Does nothing when a supported file exists;
    /// throws InvalidDataException without writing when it is unreadable or unsupported.
    /// Throws NoRepositoryFolderException when no repository is selected.
    /// </summary>
    void EnsureSettings();

    /// <summary>
    /// Deletes tasks whose resolution is strictly older than the shared delay.
    /// Uses the existing task deletion behavior, including image cleanup and failure reporting.
    /// Throws before deleting anything when settings cannot be read or no repository is selected.
    /// </summary>
    void DeleteExpiredTasks();
}
