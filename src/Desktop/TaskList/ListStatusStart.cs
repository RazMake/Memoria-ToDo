using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.KeepAwake;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The start of Task View's status bar: the Sync button and a failed sync's reason on the left, the app's version at the
/// right edge of the width Updates' status bar gives it, which ends where the update text begins, and just left of the
/// version the Prevent screen saver sun, which follows Keep awake while the bar shows. The Task list passes it
/// to that bar as its start. It is not a tab stop; F5 syncs from anywhere in the window.
/// </summary>
internal sealed class ListStatusStart : DockPanel
{
    private readonly IKeepAwakeStatus _keepAwake;
    private readonly TextBlock _sun = new()
    {
        Text = "\uE706",
        FontFamily = StatusBarText.IconFont,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0),
    };

    public ListStatusStart(ListSync sync, IKeepAwakeStatus keepAwake)
    {
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(keepAwake);
        _keepAwake = keepAwake;
        Focusable = false;
        KeyboardNavigation.SetIsTabStop(this, false);
        _sun.Focusable = false;
        KeyboardNavigation.SetIsTabStop(_sun, false);

        var version = new TextBlock { Text = StatusBarText.Version, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        version.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        SetDock(version, Dock.Right);
        SetDock(_sun, Dock.Right);
        Children.Add(version);
        Children.Add(_sun);
        Children.Add(sync);
        Show();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // Keep awake lives for the app's life and Task View is rebuilt on every open, so the bar follows it only while it shows.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _keepAwake.Changed += OnChanged;
        Show();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _keepAwake.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => Show();

    private void Show()
    {
        var state = _keepAwake.State;
        var tooltip = StatusBarText.KeepAwakeTooltip(state);
        _sun.ToolTip = tooltip;
        AutomationProperties.SetName(_sun, tooltip);
        _sun.SetResourceReference(TextBlock.ForegroundProperty, state switch
        {
            { Failure: not null } => "SystemFillColorCriticalBrush",
            { IsOn: true, Paused: false } => "TextFillColorPrimaryBrush",
            _ => "TextFillColorSecondaryBrush",
        });
    }
}
