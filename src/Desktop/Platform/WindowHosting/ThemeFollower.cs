using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// Calls themeChanged when the theme switches, while element is loaded, and once each time it loads.
/// Disposing it stops the calls.
/// </summary>
public sealed class ThemeFollower : IDisposable
{
    private readonly FrameworkElement _element;
    private readonly ITheme _theme;
    private readonly Action _themeChanged;
    private bool _subscribed;
    private bool _disposed;

    /// <summary>Throws <see cref="ArgumentNullException"/> for a null argument.</summary>
    public ThemeFollower(FrameworkElement element, ITheme theme, Action themeChanged)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(themeChanged);
        _element = element;
        _theme = theme;
        _themeChanged = themeChanged;
        element.Loaded += OnLoaded;
        element.Unloaded += OnUnloaded;
        if (element.IsLoaded)
        {
            Subscribe();
            _themeChanged();
        }
    }

    /// <inheritdoc />
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
