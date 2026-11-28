namespace Memoria.ToDo.Shared.Platform;

/// <summary>What Sync uses.</summary>
public interface ISyncedRepository
{
    /// <summary>The selected folder, or null.</summary>
    string? Folder { get; }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/>, before update runs, when expectedFolder isn't selected
    /// or when called under the lock.
    /// Reloads every part under the lock, also when update fails, then raises the notices of each part
    /// that reloaded; a part that failed keeps its state and raises none. One failure is thrown as it is;
    /// several make an <see cref="AggregateException"/>: the update's first, then each part's reload in the order they were added,
    /// then each part's notices in the same order.
    /// </summary>
    void Update(string expectedFolder, Action update);

    /// <summary>Every part's skipped files, in the order the parts were added.</summary>
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }

    /// <summary>Raised after a store's local change, outside the lock.</summary>
    event EventHandler? LocalSaved;
}
