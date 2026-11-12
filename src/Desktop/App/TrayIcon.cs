using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The notification-area icon, with the app icon, a tooltip, a context menu, and a left-click action.</summary>
internal sealed class TrayIcon : ITrayIcon
{
    private const string IconResourceName = "app.ico";

    private volatile Shown? _shown;

    public void Show(string toolTip, IReadOnlyList<TrayMenuItem> menu, Action leftClick)
    {
        ArgumentNullException.ThrowIfNull(toolTip);
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(leftClick);
        if (_shown is not null)
        {
            throw new InvalidOperationException("The tray icon is already shown.");
        }

        var shown = new Shown(LoadIcon());
        var icon = shown.Icon;
        icon.ToolTipText = toolTip;
        icon.ContextMenu = BuildMenu(menu);
        icon.NoLeftClickDelay = true;
        icon.TrayLeftMouseUp += (_, _) => leftClick();
        icon.TrayBalloonTipClicked += (_, _) => shown.ErrorClicked?.Invoke();
        try
        {
            shown.ShowBadge(TraySyncBadge.None);
            icon.ForceCreate(enablesEfficiencyMode: false);
            if (!icon.IsCreated)
            {
                throw new InvalidOperationException("Windows couldn't add the tray icon.");
            }
        }
        catch
        {
            shown.Dispose();
            throw;
        }

        _shown = shown;
    }

    public void UpdateStatus(string toolTip, TraySyncBadge badge)
    {
        ArgumentNullException.ThrowIfNull(toolTip);
        var shown = _shown;
        shown?.Icon.Dispatcher.BeginInvoke(() =>
        {
            if (ReferenceEquals(_shown, shown))
            {
                shown.Icon.ToolTipText = toolTip;
                shown.ShowBadge(badge);
            }
        });
    }

    public void NotifySyncError(string message, Action clicked)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(clicked);
        var shown = _shown;
        shown?.Icon.Dispatcher.BeginInvoke(() =>
        {
            if (ReferenceEquals(_shown, shown))
            {
                shown.ErrorClicked = clicked;
                shown.Icon.ShowNotification(AppName.Value, message, NotificationIcon.Error);
            }
        });
    }

    public void Remove()
    {
        var shown = _shown;
        _shown = null;
        shown?.Dispose();
    }

    /// <summary>The created icon with the images it shows. Used on the UI thread, except for the dispatcher read.</summary>
    private sealed class Shown : IDisposable
    {
        private readonly System.Drawing.Icon _plain;
        private IntPtr _badgedHandle;

        public Shown(System.Drawing.Icon plain)
        {
            _plain = plain;
            Icon = new TaskbarIcon();
        }

        public TaskbarIcon Icon { get; }

        public Action? ErrorClicked { get; set; }

        public void ShowBadge(TraySyncBadge badge)
        {
            var oldHandle = _badgedHandle;
            if (badge == TraySyncBadge.None)
            {
                _badgedHandle = IntPtr.Zero;
                Icon.Icon = _plain;
            }
            else
            {
                _badgedHandle = DrawBadged(_plain, badge);
                Icon.Icon = System.Drawing.Icon.FromHandle(_badgedHandle);
            }

            if (oldHandle != IntPtr.Zero)
            {
                DestroyIcon(oldHandle);
            }
        }

        public void Dispose()
        {
            Icon.Dispose();
            if (_badgedHandle != IntPtr.Zero)
            {
                DestroyIcon(_badgedHandle);
                _badgedHandle = IntPtr.Zero;
            }

            _plain.Dispose();
        }

        // The app icon with a dot in its lower-right corner: amber for offline, red for a warning.
        private static IntPtr DrawBadged(System.Drawing.Icon plain, TraySyncBadge badge)
        {
            using var bitmap = plain.ToBitmap();
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                var diameter = Math.Max(bitmap.Width / 2, 4);
                var colour = badge == TraySyncBadge.Warning ? System.Drawing.Color.Red : System.Drawing.Color.Orange;
                using var brush = new System.Drawing.SolidBrush(colour);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillEllipse(brush, bitmap.Width - diameter, bitmap.Height - diameter, diameter - 1, diameter - 1);
            }

            return bitmap.GetHicon();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);
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
