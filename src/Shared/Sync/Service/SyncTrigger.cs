namespace Memoria.ToDo.Shared.Sync;

/// <summary>What started a sync pass, as the log names it.</summary>
internal enum SyncTrigger
{
    /// <summary>The service started.</summary>
    Start,

    /// <summary>The user asked for a sync.</summary>
    Manual,

    /// <summary>An edit was saved and its debounce ended.</summary>
    Edit,

    /// <summary>The wait after a failed pass ended.</summary>
    Retry,

    /// <summary>The periodic interval ended.</summary>
    Periodic,
}
