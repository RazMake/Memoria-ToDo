using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Shows the hand pointer over push buttons and toggle buttons, but not check boxes, radio buttons or repeat buttons.</summary>
internal static class ButtonCursor
{
    private static bool _registered;

    /// <summary>Registers the one class-wide handler; later calls in the same process do nothing. UI thread only.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventManager.RegisterClassHandler(typeof(ButtonBase), Mouse.QueryCursorEvent, new QueryCursorEventHandler(OnQueryCursor));
    }

    private static void OnQueryCursor(object sender, QueryCursorEventArgs e)
    {
        if (sender is System.Windows.Controls.Button or ToggleButton and not (System.Windows.Controls.CheckBox or System.Windows.Controls.RadioButton))
        {
            e.Cursor = Cursors.Hand;
            e.Handled = true;
        }
    }
}
