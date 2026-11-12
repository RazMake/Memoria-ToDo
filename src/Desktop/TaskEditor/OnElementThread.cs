using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

internal static class OnElementThread
{
    /// <summary>
    /// Runs <paramref name="action"/> now when called on the thread that owns <paramref name="element"/>, otherwise queues it
    /// there. A queued action is dropped if the element was unloaded meanwhile; loading it again catches up.
    /// </summary>
    public static void Run(FrameworkElement element, Action action)
    {
        if (element.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        element.Dispatcher.BeginInvoke(() =>
        {
            if (element.IsLoaded)
            {
                action();
            }
        });
    }
}
