using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>At most one window at a time: made when first shown, and again after the user closes it.</summary>
public sealed class SingleWindow
{
    private readonly Func<Window> _createWindow;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private Window? _window;

    /// <summary>
    /// Makes no window yet. <paramref name="createWindow"/> makes a new, hidden window each time it's called.
    /// Create this object on the UI thread.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="createWindow"/> is null.</exception>
    public SingleWindow(Func<Window> createWindow)
    {
        ArgumentNullException.ThrowIfNull(createWindow);
        _createWindow = createWindow;
    }

    /// <summary>
    /// With no window yet, or after the user closed the last one: makes one with the function, then opens it with
    /// <see cref="WindowPlacement.ShowOnPointerScreen"/>. Otherwise passes the window it has to
    /// <see cref="WindowPlacement.ShowOnPointerScreen"/>, which brings an open one forward.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Called off the thread that created this object, before making a window; or the function returned null.
    /// </exception>
    /// <exception cref="Win32Exception">Windows can't give the pointer's position, or its screen's work area or scale.</exception>
    /// <remarks>
    /// The function's exceptions pass on and no window is kept. If opening the window fails, the window is kept,
    /// so the next call tries again.
    /// </remarks>
    public void Show()
    {
        _dispatcher.VerifyAccess();

        if (_window is null)
        {
            var window = _createWindow() ?? throw new InvalidOperationException("The function made no window.");
            window.Closed += (_, _) => _window = null;
            _window = window;
        }

        WindowPlacement.ShowOnPointerScreen(_window);
    }
}
