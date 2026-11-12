using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// Shows sync status on the tray icon: a badge and tooltip for every status change, and one notification for each new
/// kind of error until a clean success. Status changes arrive on any thread; the icon's adapter owns UI dispatch.
/// </summary>
internal sealed class SyncTray(ISyncService sync, ITrayIcon trayIcon, Action openConfigure)
{
    private readonly object gate = new();
    private readonly HashSet<SyncFailure> notified = [];
    private bool attached;

    /// <summary>Shows the current status and follows later changes.</summary>
    public void Attach()
    {
        lock (gate)
        {
            attached = true;
        }

        sync.StatusChanged += OnStatusChanged;
        Show(sync.Status);
    }

    /// <summary>Stops following changes. A change already running may still reach the icon, which discards it after Remove.</summary>
    public void Detach()
    {
        sync.StatusChanged -= OnStatusChanged;
        lock (gate)
        {
            attached = false;
        }
    }

    private void OnStatusChanged(object? sender, EventArgs e) => Show(sync.Status);

    private void Show(ISyncStatus status)
    {
        bool notify = false;
        SyncFailure failure = status.Failure ?? SyncFailure.Unexpected;
        lock (gate)
        {
            if (!attached)
            {
                return;
            }

            if (status.State == SyncState.Idle)
            {
                notified.Clear();
            }
            else if (status.State == SyncState.Error)
            {
                notify = notified.Add(failure);
            }
        }

        var badge = status.State switch
        {
            SyncState.Offline => TraySyncBadge.Offline,
            SyncState.Error => TraySyncBadge.Warning,
            _ => TraySyncBadge.None,
        };
        trayIcon.UpdateStatus(ToolTip(status), badge);

        if (notify)
        {
            trayIcon.NotifySyncError(ErrorText(status), openConfigure);
        }
    }

    // What an error says: Sync's own details, or its failure kind when it gave none.
    private static string ErrorText(ISyncStatus status) =>
        status.Details ?? $"Sync failed: {status.Failure ?? SyncFailure.Unexpected}.";

    private static string ToolTip(ISyncStatus status)
    {
        var lines = new List<string>(3) { AppName.Value };
        if (status.LastSuccessfulSync is { } last)
        {
            lines.Add($"Last synced {last.ToLocalTime():g}");
        }

        // Last, so a long error can't push the sync time out of a truncated tooltip.
        if (status.State == SyncState.Error)
        {
            lines.Add(ErrorText(status));
        }
        else if (status.State == SyncState.Offline && status.Details is not null)
        {
            lines.Add(status.Details);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
