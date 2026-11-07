using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The notification-area icon, with the app icon, a tooltip, a context menu, and a left-click action.</summary>
internal sealed class TrayIcon : ITrayIcon
{
    private const string IconResourceName = "app.ico";

    private TaskbarIcon? _icon;

    public void Show(string toolTip, IReadOnlyList<TrayMenuItem> menu, Action leftClick)
    {
        ArgumentNullException.ThrowIfNull(toolTip);
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(leftClick);
        if (_icon is not null)
        {
            throw new InvalidOperationException("The tray icon is already shown.");
        }

        var icon = new TaskbarIcon
        {
            Icon = LoadIcon(),
            ToolTipText = toolTip,
            ContextMenu = BuildMenu(menu),
            NoLeftClickDelay = true,
        };
        icon.TrayLeftMouseUp += (_, _) => leftClick();
        try
        {
            icon.ForceCreate(enablesEfficiencyMode: false);
            if (!icon.IsCreated)
            {
                throw new InvalidOperationException("Windows couldn't add the tray icon.");
            }
        }
        catch
        {
            icon.Dispose();
            throw;
        }

        _icon = icon;
    }

    public void Remove()
    {
        var icon = _icon;
        _icon = null;
        icon?.Dispose();
    }

    private static ContextMenu BuildMenu(IReadOnlyList<TrayMenuItem> menu)
    {
        var contextMenu = new ContextMenu();
        foreach (var item in menu)
        {
            var menuItem = new MenuItem { Header = item.Text };
            menuItem.Click += (_, _) => item.Invoke();
            contextMenu.Items.Add(menuItem);
        }

        return contextMenu;
    }

    private static System.Drawing.Icon LoadIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(IconResourceName)
            ?? throw new InvalidOperationException($"The icon resource '{IconResourceName}' is missing.");

        // The image closest to the notification area's icon size in pixels. The tray icon library passes the
        // icon's handle on as it is, so a different size would be scaled by Windows.
        return new System.Drawing.Icon(stream, NotificationAreaIconSize.Get());
    }
}
