namespace Memoria.ToDo.Shared.Platform;

/// <summary>What a store uses.</summary>
public interface IRepositoryAccess
{
    /// <summary>The selected folder, or null.</summary>
    string? Folder { get; }

    /// <summary>
    /// Adds a part. Throws <see cref="InvalidOperationException"/> after the first Select, even one that failed,
    /// and <see cref="ArgumentException"/> when the part was already added.
    /// </summary>
    void Add(IRepositoryPart part);

    /// <summary>Runs the call under the lock with the selected folder, which may be null. Reentrant on one thread.</summary>
    T Locked<T>(Func<string?, T> call);

    /// <inheritdoc cref="Locked{T}(Func{string?, T})"/>
    void Locked(Action<string?> call);

    /// <summary>
    /// Runs a change that raises notices under the lock with the selected folder.
    /// Throws <see cref="InvalidOperationException"/>, before running, when the thread already holds the lock.
    /// </summary>
    T LockedChange<T>(Func<string?, T> call);

    /// <inheritdoc cref="LockedChange{T}(Func{string?, T})"/>
    void LockedChange(Action<string?> call);

    /// <summary>
    /// Raises LocalSaved on the calling thread, after a change Sync should carry.
    /// Throws <see cref="InvalidOperationException"/> when called under the lock.
    /// </summary>
    void ReportLocalSave();
}
