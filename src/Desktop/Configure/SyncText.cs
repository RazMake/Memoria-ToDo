using System.Globalization;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The three read-only lines about the repository: its address, the local folder, and the device's identity.</summary>
internal readonly record struct RepositoryLines(string Address, string Folder, string Identity);

/// <summary>The words the Sync settings show for the setup and the sync status.</summary>
internal static class SyncText
{
    /// <summary>The repository lines of the setup; a missing address or folder reads "None".</summary>
    public static RepositoryLines Repository(ISyncSetupState state) =>
        new(state.RepositoryAddress ?? "None", state.RepositoryFolder ?? "None", state.DeviceName);

    /// <summary>The status: the last success, pending changes, why the sync fails, and the skipped files, one per line.</summary>
    public static string Status(ISyncStatus status)
    {
        var lines = new List<string>
        {
            status.LastSuccessfulSync is { } at
                ? $"Last successful sync: {at.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}"
                : "Not synced yet.",
        };
        if (status.HasPendingChanges)
        {
            lines.Add("Changes wait to be synced.");
        }

        if (IsFailure(status) && status.Details is not null)
        {
            lines.Add(status.Details);
        }

        lines.AddRange(status.SkippedFiles.Select(file => $"Skipped {file.RelativePath}: {file.Reason}"));
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>The host key the device trusts or waits to trust, on one line.</summary>
    public static string HostKey(ISshHostKey host) =>
        $"Host {host.Host}:{host.Port} key {host.Algorithm} {host.Fingerprint}";

    /// <summary>Whether the sync failed, or can't reach the host.</summary>
    public static bool IsFailure(ISyncStatus status) => status.State is SyncState.Error or SyncState.Offline;
}
