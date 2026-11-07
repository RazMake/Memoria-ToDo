namespace Memoria.ToDo.Desktop.Shell;

/// <summary>The tray icon, implemented by the composition root.</summary>
public interface ITrayIcon
{
    /// <summary>Shows the icon with its tooltip, menu, and the action for a left click.</summary>
    void Show(string toolTip, IReadOnlyList<TrayMenuItem> menu, Action leftClick);

    /// <summary>Removes the icon.</summary>
    void Remove();
}

/// <summary>One tray menu item.</summary>
public sealed record TrayMenuItem(string Text, Action Invoke);
