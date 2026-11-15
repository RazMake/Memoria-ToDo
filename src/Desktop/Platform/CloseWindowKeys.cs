using System.Windows;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// The keys that close whichever of the app's windows has the focus, whatever control in it has the focus.
/// The composition root makes one for the app. Use it on the UI thread.
/// </summary>
public sealed class CloseWindowKeys
{
    private ModifierKeys _modifiers;
    private Key _key = Key.None;

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
        const ModifierKeys All = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows;
        if ((modifiers & ~All) != 0)
        {
            throw new ArgumentException("Only Ctrl, Alt, Shift, and Win can be modifiers.", nameof(modifiers));
        }

        if (key is Key.None or Key.System || ModifierKey.Of(key) != ModifierKeys.None)
        {
            throw new ArgumentException("The key can't be none or a modifier key.", nameof(key));
        }

        _modifiers = modifiers;
        _key = key;
    }

    /// <summary>
    /// From now on, pressing the current keys while window, or any control in it, has the focus calls
    /// window.Close() before any control sees the keys, and the keys go no further.
    /// Throws <see cref="ArgumentNullException"/> when window is null.
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
                window.Close();
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
