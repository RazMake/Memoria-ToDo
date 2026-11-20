using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Closing a popup, which is a window of its own and doesn't move with its owner, when its owner's window goes away or moves.</summary>
internal static class PopupClosers
{
    /// <summary>
    /// While <paramref name="owner"/> is on screen, calls <paramref name="close"/> when its window is deactivated, moved, resized,
    /// minimized, or hidden, and when the owner is unloaded. <paramref name="close"/> must be safe to call when nothing is open.
    /// </summary>
    public static void Attach(FrameworkElement owner, Action close)
    {
        Window? window = null;
        var closeOnEvent = new EventHandler((_, _) => close());
        var closeOnSize = new SizeChangedEventHandler((_, _) => close());
        var closeOnVisibility = new DependencyPropertyChangedEventHandler((_, _) => close());

        void Follow()
        {
            Unfollow();
            window = Window.GetWindow(owner);
            if (window is null)
            {
                return;
            }

            window.Deactivated += closeOnEvent;
            window.LocationChanged += closeOnEvent;
            window.StateChanged += closeOnEvent;
            window.SizeChanged += closeOnSize;
            window.IsVisibleChanged += closeOnVisibility;
        }

        void Unfollow()
        {
            if (window is not null)
            {
                window.Deactivated -= closeOnEvent;
                window.LocationChanged -= closeOnEvent;
                window.StateChanged -= closeOnEvent;
                window.SizeChanged -= closeOnSize;
                window.IsVisibleChanged -= closeOnVisibility;
                window = null;
            }

            close();
        }

        WhileLoaded.Run(owner, Follow, Unfollow);
        if (owner.IsLoaded)
        {
            Follow();
        }
    }
}
