using Serilog;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>How Sync logs a failure that ends a pass or a setup action: offline at Warning, every other kind at Error.</summary>
internal static class SyncFailureLog
{
    /// <summary>Logs the failure with its kind and details, and its exception when there is one.</summary>
    public static void LogFailure(this ILogger logger, SyncFailure kind, string details, Exception? failure)
    {
        if (kind == SyncFailure.Connection)
            logger.Warning(failure, "Sync is offline ({Kind}): {Details}", kind, details);
        else
            logger.Error(failure, "Sync failed ({Kind}): {Details}", kind, details);
    }
}
