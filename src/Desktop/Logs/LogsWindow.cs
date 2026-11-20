using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>Creates the Logs window.</summary>
public static class LogsWindow
{
    /// <summary>Creates the Logs window. It opens only on <see cref="ILogsWindow.Show"/>.</summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static ILogsWindow Create(ILogSource source, ITheme theme, CloseWindowKeys closeWindowKeys, TimeProvider time, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(closeWindowKeys);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        return new Opener(source, closeWindowKeys, time, logger);
    }

    private sealed class Opener : ILogsWindow
    {
        private readonly SingleWindow _window;
        private LogsMainWindow? _current;

        public Opener(ILogSource source, CloseWindowKeys closeWindowKeys, TimeProvider time, ILogger logger) =>
            _window = new SingleWindow(() =>
            {
                var window = new LogsMainWindow(source, time, logger);
                closeWindowKeys.Attach(window);
                _current = window;
                window.Closed += (_, _) => _current = null;
                return window;
            });

        public void Show()
        {
            var wasOpen = _window.IsOpen;
            _window.Show();
            if (!wasOpen)
            {
                _current?.Reload();
            }
        }
    }
}
