using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>A text field that can say where a place in its text shows, for a popup that opens there.</summary>
internal interface IPopupPlace
{
    /// <summary>The element the rectangles <see cref="AnchorOf"/> gives are in the coordinates of.</summary>
    UIElement Target { get; }

    /// <summary>The box the popup opens under for the text offset, or null when that place is out of the field's view.</summary>
    Rect? AnchorOf(int offset);

    /// <summary>Raised when the field's text scrolls.</summary>
    event Action Scrolled;
}
