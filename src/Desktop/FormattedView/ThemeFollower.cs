using System.Windows;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>
/// Calls back when the theme changes, but only while the element is loaded, so a window that is closed holds no subscription.
/// It also calls back once the element loads, since the theme may have changed while it was away.
/// </summary>
internal sealed class ThemeFollower : IDisposable
{
    private readonly FrameworkElement _element;
    private readonly ITheme _theme;
    private readonly Action _themeChanged;
    private bool _subscribed;
    private bool _disposed;

    public ThemeFollower(FrameworkElement element, ITheme theme, Action themeChanged)
    {
        _element = element;
        _theme = theme;
        _themeChanged = themeChanged;
        element.Loaded += OnLoaded;
        element.Unloaded += OnUnloaded;
        if (element.IsLoaded)
        {
            Subscribe();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _element.Loaded -= OnLoaded;
        _element.Unloaded -= OnUnloaded;
        Unsubscribe();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Subscribe();
        _themeChanged();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Unsubscribe();

    private void OnChanged(object? sender, EventArgs e) => _themeChanged();

    private void Subscribe()
    {
        if (!_subscribed && !_disposed)
        {
            _theme.Changed += OnChanged;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed)
        {
            _theme.Changed -= OnChanged;
            _subscribed = false;
        }
    }
}
