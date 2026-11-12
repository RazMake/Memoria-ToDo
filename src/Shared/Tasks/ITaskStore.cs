namespace Memoria.ToDo.Shared.Tasks;

/// <summary>One store's task operations and the separate capabilities used by Configure and Sync.</summary>
public interface ITaskStore : ITasks
{
    /// <summary>The settings in this store's selected repository.</summary>
    ISharedTaskSettings Settings { get; }

    /// <summary>Sync's access to this same store, including its local update lock.</summary>
    ITaskSynchronization Synchronization { get; }
}
