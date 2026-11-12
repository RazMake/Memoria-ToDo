namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// The app's existing selected-folder owner. Sync calls it on its worker, loading during initialization
/// and selecting only after validating the clone.
/// The adapter persists the selection once and makes that same folder current in Tasks.
/// It performs no Git or network work and does not create another selected-path setting.
/// </summary>
public interface IRepositorySelection
{
    /// <summary>
    /// Loads the saved selection and makes that folder current in Tasks, before Sync publishes its initial setup.
    /// A missing selection file means no selected folder; other read or adoption failures propagate without
    /// replacing the last valid selection. Construction performs no selection I/O.
    /// </summary>
    void Load();

    /// <summary>
    /// Saves and adopts the new folder. If adoption fails, restores the previous saved selection;
    /// a failed restoration reports both failures.
    /// </summary>
    void Select(string folder);
}
