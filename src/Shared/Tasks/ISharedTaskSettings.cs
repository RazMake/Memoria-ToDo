namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// Settings shared by every device. Safe to call from any thread; uses the task store's lock.
/// A missing file supplies defaults. An unreadable or unsupported file makes reads and writes
/// throw InvalidDataException rather than replace it or use defaults for automatic deletion.
/// </summary>
public interface ISharedTaskSettings
{
    /// <summary>The number of whole days a resolved task is kept, from 1 to 365.</summary>
    int DeletionDelayDays { get; }

    /// <summary>Shortcut text Windows interprets; null means Windows' existing default.</summary>
    string? AddNewTaskShortcut { get; }

    /// <summary>Shortcut text Windows interprets; null means Windows' existing default.</summary>
    string? ManageTasksShortcut { get; }

    /// <summary>
    /// Raised on the calling thread, outside the store's lock, after settings change locally,
    /// on repository selection, or through reload. No-op setters raise nothing.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Saves the delay and its UTC change time. Throws ArgumentOutOfRangeException outside 1–365,
    /// or NoRepositoryFolderException when no repository is selected. An unchanged value writes nothing.
    /// </summary>
    void SetDeletionDelayDays(int days);

    /// <summary>
    /// Saves text without interpreting it, or null to use Windows' default, and its UTC change time.
    /// Throws NoRepositoryFolderException when no repository is selected. An unchanged value writes nothing.
    /// </summary>
    void SetAddNewTaskShortcut(string? shortcut);

    /// <summary>
    /// Saves text without interpreting it, or null to use Windows' default, and its UTC change time.
    /// Throws NoRepositoryFolderException when no repository is selected. An unchanged value writes nothing.
    /// </summary>
    void SetManageTasksShortcut(string? shortcut);
}
