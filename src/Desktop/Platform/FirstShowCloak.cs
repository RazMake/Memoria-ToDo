using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// Keeps a window shown for the first time invisible until WPF has drawn it, so it never shows a white frame before
/// the theme's colors. Windows puts a new window on the screen before WPF draws into it.
/// </summary>
internal static class FirstShowCloak
{
    /// <summary>Whether the window has never been shown. Ask before anything creates its handle.</summary>
    public static bool IsFirstShow(Window window) => PresentationSource.FromVisual(window) is null;

    /// <summary>
    /// Cloaks the window now, and uncloaks it after it has loaded and WPF has drawn it, at the step where WPF raises
    /// ContentRendered. Call it just before showing a window for which <see cref="IsFirstShow"/> was true.
    /// </summary>
    /// <exception cref="Win32Exception">Windows can't cloak the window.</exception>
    public static void Start(Window window, Serilog.ILogger logger)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        WindowCloak.Set(handle, true);

        var closed = false;
        window.Closed += (_, _) => closed = true;

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            window.RemoveHandler(FrameworkElement.LoadedEvent, (RoutedEventHandler)OnLoaded);

            // BeginInvoke, not InvokeAsync: a failure must reach the dispatcher's unhandled-error handling.
            window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                // A closed window's handle is gone, and the window is already off the screen.
                if (!closed)
                {
                    WindowCloak.Set(handle, false);
                }

                logger.Debug("Finished the first show of the window {Window}; Uncloaked {Uncloaked}", window.GetType().Name, !closed);
            }));
        }

        // handledEventsToo: a Loaded handler that marks the event handled must not leave the window cloaked.
        window.AddHandler(FrameworkElement.LoadedEvent, (RoutedEventHandler)OnLoaded, handledEventsToo: true);
    }
}
