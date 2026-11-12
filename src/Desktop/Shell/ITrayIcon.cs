namespace Memoria.ToDo.Desktop.Shell;

/// <summary>The tray icon, implemented by the composition root.</summary>
public interface ITrayIcon
{
    /// <summary>Shows the icon with its tooltip, menu, and the action for a left click.</summary>
    void Show(string toolTip, IReadOnlyList<TrayMenuItem> menu, Action leftClick);

    /// <summary>
    /// Updates the existing icon from any thread; the adapter dispatches to its UI thread.
    /// Ignored after <see cref="Remove"/>, as <see cref="Remove"/> describes.
    /// </summary>
    void UpdateStatus(string toolTip, TraySyncBadge badge);

    /// <summary>
    /// Shows a sync-error notification from any thread. Its click runs on the UI thread.
    /// The adapter owns dispatch failures through the app's normal error handling.
    /// Ignored after <see cref="Remove"/>, as <see cref="Remove"/> describes.
    /// </summary>
    void NotifySyncError(string message, Action clicked);

    /// <summary>
    /// Removes the icon. Every queued, in-flight, and later <see cref="UpdateStatus"/> and
    /// <see cref="NotifySyncError"/> call is then ignored and never recreates the icon.
    /// </summary>
    void Remove();
}

/// <summary>One tray menu item.</summary>
public sealed record TrayMenuItem(string Text, Action Invoke);

public enum TraySyncBadge
{
    None,
    Offline,
    Warning,
}
