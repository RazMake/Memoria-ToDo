using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The start of Task View's status bar: the Sync button and a failed sync's reason on the left, the app's version at the
/// right edge of the width Updates' status bar gives it, which ends where the update text begins. The Task list passes it
/// to that bar as its start. It is not a tab stop; F5 syncs from anywhere in the window.
/// </summary>
internal sealed class ListStatusStart : DockPanel
{
    public ListStatusStart(ListSync sync)
    {
        ArgumentNullException.ThrowIfNull(sync);
        Focusable = false;
        KeyboardNavigation.SetIsTabStop(this, false);

        var version = new TextBlock { Text = StatusBarText.Version, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        version.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        SetDock(version, Dock.Right);
        Children.Add(version);
        Children.Add(sync);
    }
}
