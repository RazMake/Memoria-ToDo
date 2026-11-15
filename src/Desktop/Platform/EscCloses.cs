using System.Windows;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Makes Esc close a window, unless a control used the key first.</summary>
public static class EscCloses
{
    /// <summary>
    /// Closes the window when an Esc key press, with no modifier key held, reaches it with no control having handled it.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is null.</exception>
    public static void Attach(Window window) => Attach(window, () => Keyboard.Modifiers);

    /// <summary>
    /// Like <see cref="Attach(Window)"/>, but also keeps the window open while <paramref name="physicalModifiers"/>
    /// reports a held modifier, including one pressed before the window got focus. Windows counts as a modifier.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> or <paramref name="physicalModifiers"/> is null.</exception>
    internal static void Attach(Window window, Func<ModifierKeys> physicalModifiers)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(physicalModifiers);

        var held = new HeldModifierKeys();
        // Modifier keys are tracked even when a control handled them, so the held set stays true.
        window.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, e) =>
        {
            var key = EventKey.Of(e);
            if (ModifierKey.Of(key) != ModifierKeys.None)
            {
                held.Pressed(key);
            }
        }), handledEventsToo: true);
        window.AddHandler(UIElement.PreviewKeyUpEvent, new KeyEventHandler((_, e) => held.Released(EventKey.Of(e))), handledEventsToo: true);
        window.Deactivated += (_, _) => held.Clear();
        window.KeyDown += (_, e) =>
        {
            if (!e.Handled && EventKey.Of(e) == Key.Escape && !held.AnyHeld && physicalModifiers() == ModifierKeys.None)
            {
                e.Handled = true;
                window.Close();
            }
        };
    }
}
