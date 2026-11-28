using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Where the popup of the "@" list sits.</summary>
internal static class PopupAnchor
{
    /// <summary>
    /// The box the popup opens under, aligned with its left edge: the "@"'s own box while any of it is in the field's view, else
    /// null, since the "@" scrolled out and the list must close. A box that only touches the view's edge is out of view.
    /// Both rectangles are in the same coordinates.
    /// </summary>
    public static Rect? At(Rect atSign, Rect visible) =>
        atSign.Right > visible.Left && atSign.Left < visible.Right && atSign.Bottom > visible.Top && atSign.Top < visible.Bottom
            ? atSign
            : null;
}
