using System.Windows;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The theme Windows' light or dark setting sets for the app's windows.</summary>
public sealed class WindowsTheme : ITheme
{
    private readonly IThemeSetting _setting;
    private readonly Dispatcher _dispatcher;
    private bool _stopped;

    /// <summary>Reads <paramref name="setting"/> and watches it. Throws <see cref="InvalidOperationException"/> off a UI thread with a WPF application.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="setting"/> is null.</exception>
    /// <remarks>
    /// Watches the setting until the application's dispatcher finishes shutting down, then stops.
    /// </remarks>
    public WindowsTheme(IThemeSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        var application = Application.Current
            ?? throw new InvalidOperationException("The theme needs a WPF application.");
        _dispatcher = application.Dispatcher;
        _dispatcher.VerifyAccess();
        _setting = setting;

        // Watch before reading, so a switch in between is queued and reconciled on this thread, not lost.
        _setting.MayHaveChanged += OnMayHaveChanged;
        try
        {
            IsDark = _setting.ReadIsDark();
            _dispatcher.ShutdownFinished += StopWatching;
        }
        catch
        {
            StopWatching();
            throw;
        }
    }

    /// <inheritdoc />
    public bool IsDark { get; private set; }

    /// <summary>Raised on the thread that made this object.</summary>
    public event EventHandler? Changed;

    // Runs on any thread: hand over to the UI thread, which reads the setting itself. BeginInvoke, unlike InvokeAsync,
    // sends a failure of the refresh or of a Changed handler to the dispatcher's unhandled-exception handling.
    private void OnMayHaveChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke((Action)Refresh);

    private void Refresh()
    {
        if (_stopped)
        {
            return; // A refresh queued before the theme stopped watching.
        }

        var isDark = _setting.ReadIsDark();
        if (isDark == IsDark)
        {
            return;
        }

        IsDark = isDark;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void StopWatching(object? sender, EventArgs e) => StopWatching();

    private void StopWatching()
    {
        _stopped = true;
        _setting.MayHaveChanged -= OnMayHaveChanged;
        _dispatcher.ShutdownFinished -= StopWatching;
    }
}
