using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The Sync icon button and the line beside it for one Task View window. It shows what <see cref="SyncRequests"/>
/// says: that a pass is running, and why a pass it requested failed. The icon is the palette's blue for the theme; while a
/// pass runs the button is the theme's success green and off, and the line says "Syncing…".
/// </summary>
internal sealed class ListSync : StackPanel, IDisposable
{
    private const string BlueName = "blue";

    private readonly SyncRequests _requests;
    private readonly IReadOnlyList<PaletteColor> _palette;
    private readonly ITheme _theme;
    private readonly Button _button = new()
    {
        Content = "\uE895",
        FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        Padding = new Thickness(8, 3, 8, 3),
        Margin = new Thickness(0, 0, 8, 0),
    };
    private readonly TextBlock _line = new() { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly ControlTemplate _workingLook = WorkingLook();

    public ListSync(ISyncService service, IReadOnlyList<PaletteColor> palette, ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(theme);
        _palette = palette;
        _theme = theme;
        _requests = new SyncRequests(service, post => Dispatcher.InvokeAsync(post), Show);
        AutomationProperties.SetName(_button, "Sync");
        _button.Click += async (_, _) => await SyncAsync();
        _theme.Changed += OnThemeChanged;
        Orientation = Orientation.Horizontal;
        Children.Add(_button);
        Children.Add(_line);
        Show();
    }

    /// <inheritdoc cref="SyncRequests.SyncAsync"/>
    public Task SyncAsync() => _requests.SyncAsync();

    public void Dispose()
    {
        _theme.Changed -= OnThemeChanged;
        _requests.Dispose();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Show();

    private void Show()
    {
        var working = _requests.IsWorking;
        _button.IsEnabled = !working;
        _button.ToolTip = StatusBarText.SyncTooltip(working);
        _line.Text = working ? StatusBarText.Syncing : _requests.FailureText;
        _line.SetResourceReference(TextBlock.ForegroundProperty, working ? "TextFillColorSecondaryBrush" : "SystemFillColorCriticalBrush");
        if (working)
        {
            _button.Template = _workingLook;
            _button.SetResourceReference(Control.BackgroundProperty, "SystemFillColorSuccessBrush");
            _button.SetResourceReference(Control.ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
        }
        else
        {
            _button.ClearValue(Control.TemplateProperty);
            _button.ClearValue(Control.BackgroundProperty);
            var blue = _palette.Single(color => color.Name == BlueName);
            _button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_theme.IsDark ? blue.Dark : blue.Light));
        }
    }

    // A plain look for the off button, so the theme's greyed-out look doesn't replace the green.
    private static ControlTemplate WorkingLook()
    {
        var box = new FrameworkElementFactory(typeof(Border));
        box.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        box.SetBinding(Border.BackgroundProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath(nameof(Control.Background)) });
        box.SetBinding(Border.PaddingProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath(nameof(Control.Padding)) });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        box.AppendChild(content);
        return new ControlTemplate(typeof(Button)) { VisualTree = box };
    }
}