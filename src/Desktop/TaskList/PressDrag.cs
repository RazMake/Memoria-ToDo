using System.Windows;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The press-to-drag gesture a task row and a group heading share: a press, then a move with the left button still held that
/// reaches the drag distance, starts one drag. A release before that is a click. A press the element never hears the end of,
/// because the pointer left it first, is forgotten.
/// </summary>
internal sealed class PressDrag(double horizontalDistance, double verticalDistance)
{
    private Point? _pressedAt;
    private bool _dragged;

    /// <summary>Starts a gesture at the pointer's place.</summary>
    public void Press(Point at)
    {
        _pressedAt = at;
        _dragged = false;
    }

    /// <summary>Returns true, once per press, when a move with the button held reaches the drag distance from the press.</summary>
    public bool Move(Point now, bool leftButtonDown)
    {
        if (!leftButtonDown || _pressedAt is not { } from
            || !DragDistance.Passed(now.X - from.X, now.Y - from.Y, horizontalDistance, verticalDistance))
        {
            return false;
        }

        _pressedAt = null;
        _dragged = true;
        return true;
    }

    /// <summary>Forgets the press, as the pointer left the element and its release goes unheard.</summary>
    public void Leave() => _pressedAt = null;

    /// <summary>Ends the gesture, and returns whether it was a click: no drag started.</summary>
    public bool Release()
    {
        var click = !_dragged;
        _pressedAt = null;
        _dragged = false;
        return click;
    }
}
