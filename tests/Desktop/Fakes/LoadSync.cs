using System.Windows;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// Takes a control out of its window, or closes the window, and returns only once the control has raised Unloaded; or puts it
/// in a window and returns once it has raised Loaded. WPF raises these events from the dispatcher, after the change, so a test
/// that reads what a control does while off screen must wait for them. Not a fake of a port: shared test setup.
/// </summary>
public static class LoadSync
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <summary>Takes the control out of the window and waits for its Unloaded event.</summary>
    public static void Remove(Window window, FrameworkElement control)
    {
        Wait(control, unloaded: true, () => window.Content = null);
        if (control.IsLoaded)
        {
            throw new InvalidOperationException("The control is still loaded after it was taken out of the window.");
        }
    }

    /// <summary>Closes the window and waits for the control in it to raise Unloaded.</summary>
    public static void Close(Window window, FrameworkElement control)
    {
        Wait(control, unloaded: true, window.Close);
        if (control.IsLoaded)
        {
            throw new InvalidOperationException("The control is still loaded after its window closed.");
        }
    }

    /// <summary>Puts the control in the window and waits for its Loaded event.</summary>
    public static void Put(Window window, FrameworkElement control)
    {
        Wait(control, unloaded: false, () => window.Content = control);
        if (!control.IsLoaded)
        {
            throw new InvalidOperationException("The control isn't loaded after it was put in the window.");
        }
    }

    // Runs the change, then lets the dispatcher work until the event arrives; the time limit only bounds a failure.
    private static void Wait(FrameworkElement control, bool unloaded, Action change)
    {
        var frame = new DispatcherFrame();
        RoutedEventHandler handler = (_, _) => frame.Continue = false;
        var timeout = new DispatcherTimer(Limit, DispatcherPriority.Normal, (_, _) => frame.Continue = false, control.Dispatcher);
        if (unloaded)
        {
            control.Unloaded += handler;
        }
        else
        {
            control.Loaded += handler;
        }

        try
        {
            change();
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timeout.Stop();
            if (unloaded)
            {
                control.Unloaded -= handler;
            }
            else
            {
                control.Loaded -= handler;
            }
        }
    }
}
