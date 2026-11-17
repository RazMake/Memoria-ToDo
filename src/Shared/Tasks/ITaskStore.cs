namespace Memoria.ToDo.Shared.Tasks;

/// <summary>One store's task operations and the separate capabilities used by Configure and Sync.</summary>
public interface ITaskStore : ITasks
{
    /// <summary>The settings in this store's selected repository.</summary>
    ISharedTaskSettings Settings { get; }

    /// <summary>Each tag's color in this store's selected repository.</summary>
    ITagColors TagColors { get; }

    /// <summary>Each tag's place in this store's selected repository.</summary>
    ITagOrder TagOrder { get; }

    /// <summary>
    /// The full path of the folder where an open edit's images wait, laid out like the clone, so that <see cref="ITasks.SaveImage"/>'s
    /// link path names the image under it. It is outside the clone, may not exist yet, and stays the same for the store's whole life.
    /// </summary>
    string EditImagesFolder { get; }

    /// <summary>Sync's access to this same store, including its local update lock.</summary>
    ITaskSynchronization Synchronization { get; }
}
