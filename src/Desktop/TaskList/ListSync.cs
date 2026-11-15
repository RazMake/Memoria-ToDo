using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The Sync icon button and the failure text beside it for one Task View window. It shows what <see cref="SyncRequests"/>
/// says: that a pass is running, and why a pass it requested failed. The icon is the palette's blue for the theme.
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
        Margin = new Thickness(0, 0, 8, 0),
    };
    private readonly TextBlock _failure = new() { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    public ListSync(ISyncService service, IReadOnlyList<PaletteColor> palette, ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(theme);
        _palette = palette;
        _theme = theme;
        _requests = new SyncRequests(service, post => Dispatcher.InvokeAsync(post), Show);
        _failure.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        AutomationProperties.SetName(_button, "Sync");
        _button.Click += async (_, _) => await SyncAsync();
        _theme.Changed += OnThemeChanged;
        Orientation = Orientation.Horizontal;
        Children.Add(_button);
        Children.Add(_failure);
        ShowIconColor();
        Show();
    }

    /// <inheritdoc cref="SyncRequests.SyncAsync"/>
    public Task SyncAsync() => _requests.SyncAsync();

    public void Dispose()
    {
        _theme.Changed -= OnThemeChanged;
        _requests.Dispose();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ShowIconColor();

    private void ShowIconColor()
    {
        var blue = _palette.Single(color => color.Name == BlueName);
        _button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_theme.IsDark ? blue.Dark : blue.Light));
    }

    private void Show()
    {
        _failure.Text = _requests.FailureText;
        _button.ToolTip = StatusBarText.SyncTooltip(_requests.IsWorking);
    }
}
