namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Sync's access to Tasks, without Git or network operations.</summary>
public interface ITaskSynchronization
{
    /// <summary>The selected clone's folder, or null. Reading it does not reserve the selection.</summary>
    string? RepositoryFolder { get; }

    /// <summary>The shared layout and pure merge rules; these never read or write files.</summary>
    IRepositoryFileRules Files { get; }

    /// <summary>
    /// A snapshot of problems from the latest selection or reload, including settings.json.
    /// Skipping a file never deletes or rewrites it.
    /// </summary>
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }

    /// <summary>
    /// Raised after a local operation changed repository files, outside the store's lock.
    /// Includes settings and automatic deletion, but not drafts, staged edit images,
    /// repository selection, or imports through UpdateRepository.
    /// </summary>
    event EventHandler? LocalSaved;

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

    /// <summary>
    /// Checks the selected folder, then runs update synchronously under the task store's lock.
    /// Throws InvalidOperationException before calling update if expectedFolder is no longer selected.
    /// The callback must not perform network work, await, or raise application notifications.
    /// Reloads tasks, settings, and skipped-file diagnostics before releasing the lock, also when update
    /// fails; notices are raised only after release and do not raise LocalSaved.
    /// An update failure propagates; if reload also fails, AggregateException retains both failures.
    /// </summary>
    void UpdateRepository(string expectedFolder, Action update);
}
