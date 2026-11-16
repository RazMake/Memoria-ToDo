using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Runs an editor's key maps on the key presses of its elements, with the modifier keys it saw held, so the keys work
/// the same wherever the cursor is. A key an earlier map handled isn't offered to the next.
/// </summary>
internal sealed class KeyRouter
{
    private readonly HeldModifierKeys _held = new();

    /// <summary>
    /// Follows the modifier keys held while <paramref name="element"/> or what it holds has the focus, and runs
    /// <paramref name="keys"/> on every other key pressed there before the element's children see it.
    /// </summary>
    public void Listen(UIElement element, KeyMap keys)
    {
        element.PreviewKeyDown += (_, e) =>
        {
            var key = EventKey.Of(e);
            if (ModifierKey.Of(key) != ModifierKeys.None)
            {
                _held.Pressed(key);
                return;
            }

            _held.OtherKeyReached();
            Handle(keys, e);
        };
        element.PreviewKeyUp += (_, e) => _held.Released(EventKey.Of(e));
        element.IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (!(bool)e.NewValue)
            {
                _held.Clear();
            }
        };
    }

    /// <summary>Runs <paramref name="maps"/> in order on the keys pressed in <paramref name="field"/>, until one handles the key.</summary>
    public void Route(UIElement field, params KeyMap[] maps) =>
        field.PreviewKeyDown += (_, e) =>
        {
            foreach (var keys in maps)
            {
                Handle(keys, e);
            }
        };

    private void Handle(KeyMap keys, KeyEventArgs e)
    {
        if (!e.Handled && keys.TryRun(_held.Modifiers, EventKey.Of(e)))
        {
            e.Handled = true;
        }
    }
}
