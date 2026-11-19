using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// The keys that close whichever of the app's windows has the focus, whatever control in it has the focus.
/// The composition root makes one for the app. Use it on the UI thread.
/// </summary>
public sealed class CloseWindowKeys
{
    private ModifierKeys _modifiers;
    private Key _key = Key.None;

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    /// <summary>No keys yet: no key closes a window until <see cref="Set"/> is called.</summary>
    public CloseWindowKeys()
    {
    }

    /// <summary>
    /// From now on, these keys close each attached window. Throws <see cref="ArgumentException"/> when key is
    /// <see cref="Key.None"/> or a modifier key, or modifiers holds anything but Ctrl, Alt, Shift, and Win.
    /// </summary>
    public void Set(ModifierKeys modifiers, Key key)
    {
        ShortcutKeyCheck.Validate(modifiers, key);
        _modifiers = modifiers;
        _key = key;
    }

    /// <summary>
    /// From now on, pressing the current keys while window, or any control in it, has the focus calls
    /// sends window the user's close command, as its close button does, before any control sees the keys,
    /// and the keys go no further. Throws <see cref="ArgumentNullException"/> when window is null.
    /// </summary>
    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var held = new HeldModifierKeys();
        window.PreviewKeyDown += (_, e) =>
        {
            var key = EventKey.Of(e);
            if (ModifierKey.Of(key) != ModifierKeys.None)
            {
                held.Pressed(key);
            }
            else if (key == _key && held.Modifiers == _modifiers)
            {
                e.Handled = true;
                SendMessage(new WindowInteropHelper(window).Handle, CloseMessages.WmSysCommand, CloseMessages.ScClose, 0);
            }
        };
        window.PreviewKeyUp += (_, e) => held.Released(EventKey.Of(e));
        window.IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (!(bool)e.NewValue)
            {
                held.Clear();
            }
        };
    }
}
