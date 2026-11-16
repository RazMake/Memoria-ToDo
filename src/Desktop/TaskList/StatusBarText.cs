using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The words the status bar shows, apart from a failed sync's own message.</summary>
internal static class StatusBarText
{
    /// <summary>What shows after the Sync button, and in its tooltip, while a pass runs.</summary>
    public const string Syncing = "Syncing…";

    /// <summary>The Sync button's tooltip: "Syncing…" while a pass runs, "Sync (F5)" otherwise.</summary>
    public static string SyncTooltip(bool working) => working ? Syncing : "Sync (F5)";

    /// <summary>The app's version as the bar's right side shows it, such as "Version 1.4.2".</summary>
    public static string Version => "Version " + AppVersion.Value;
}
