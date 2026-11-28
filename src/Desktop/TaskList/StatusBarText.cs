using System.Windows.Media;
using Memoria.ToDo.Desktop.KeepAwake;
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

    /// <summary>The font the Sync button's icon and the Prevent screen saver sun draw their glyphs from.</summary>
    public static FontFamily IconFont { get; } = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>
    /// The sun's tooltip, which screen readers read as its name: what Prevent screen saver is doing. A failure, whatever the
    /// setting says, shows first, followed by its reason.
    /// </summary>
    public static string KeepAwakeTooltip(KeepAwakeState state) => state switch
    {
        { Failure: { } reason } => "Prevent screen saver isn't working: " + reason,
        { IsOn: false } => "Prevent screen saver is off.",
        { Paused: true } => "Prevent screen saver is paused while the PC is locked.",
        _ => "Prevent screen saver is on: the screen stays on.",
    };
}
