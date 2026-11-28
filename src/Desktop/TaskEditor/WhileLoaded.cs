using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Lets a control follow something long-lived only while it is on screen, so that thing can't keep it alive.</summary>
internal static class WhileLoaded
{
    /// <summary>
    /// Calls <paramref name="follow"/> each time <paramref name="element"/> is loaded, and <paramref name="unfollow"/> when it
    /// is unloaded. <paramref name="follow"/> also catches up with what happened meanwhile. Both must be safe to call twice in a row.
    /// </summary>
    public static void Run(FrameworkElement element, Action follow, Action unfollow)
    {
        element.Loaded += (_, _) =>
        {
            unfollow();
            follow();
        };
        element.Unloaded += (_, _) => unfollow();
    }
}
