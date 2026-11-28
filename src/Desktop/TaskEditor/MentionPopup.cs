using System.Windows;
using System.Windows.Controls.Primitives;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Shows the contact list in a popup under the "@" being typed, aligned with it, which WPF keeps on the screen. The popup never
/// takes the keyboard focus, stays open until it is closed explicitly, follows the "@" as the field scrolls, and closes when the
/// "@" scrolls out of view or the window loses the focus or moves.
/// </summary>
internal sealed class MentionPopup : Popup
{
    private readonly ChoiceList _list;
    private readonly Func<ContactPicking?> _showing;
    private readonly IReadOnlyDictionary<ITextField, IPopupPlace> _places;

    public MentionPopup(ChoiceList list, Func<ContactPicking?> showing, IReadOnlyDictionary<ITextField, IPopupPlace> places,     Func<bool> headerClicked, ITheme theme)
    {
        _list = list;
        _showing = showing;
        _places = places;
        Placement = PlacementMode.Bottom;
        StaysOpen = true;
        AllowsTransparency = true;
        var frame = PopupFrame.Make();
        frame.Child = new MentionPopupList(list, showing, headerClicked, theme);
        Child = frame;
        list.Changed += Place;
        foreach (var place in places.Values)
        {
            place.Scrolled += Place;
        }
    }

    // Opens under the "@" of the picking that shows the list, closes the list when that "@" is out of view, else closes the popup.
    private void Place()
    {
        if (_list.IsOpen
            && _showing() is { AtOffset: { } at } picking
            && _places.TryGetValue(picking.Field, out var place))
        {
            if (place.AnchorOf(at) is not { } anchor)
            {
                _list.Close();
                return;
            }

            PlacementTarget = place.Target;
            PlacementRectangle = anchor;
            IsOpen = true;
        }
        else
        {
            IsOpen = false;
        }
    }
}
