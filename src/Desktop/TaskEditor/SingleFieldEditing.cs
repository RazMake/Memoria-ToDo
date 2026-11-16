using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// What an editor of one text field does beside the field: it holds the field's text and the value it started from,
/// saves it with the failures shown under the field, and asks to save on the Save keys and to cancel on Esc once no list is open.
/// </summary>
internal sealed class SingleFieldEditing
{
    private readonly StyledTextEditor _field;
    private readonly MessageLine _message;
    private string _baseline = "";

    /// <param name="owner">The editor control, which lays out the field, the lists, and the message line.</param>
    /// <param name="requestSave">Raises the owner's SaveRequested.</param>
    /// <param name="requestCancel">Raises the owner's CancelRequested.</param>
    public SingleFieldEditing(UIElement owner, StyledTextEditor field, PickerLists lists, MessageLine message,
        SaveKeys saveKeys, KeyRouter router, Action requestSave, Action requestCancel)
    {
        _field = field;
        _message = message;
        var editorKeys = new KeyMap()
            .OnSaveKeys(saveKeys, requestSave)
            // Add's Save and clear has no counterpart here; the key does nothing rather than type a line break.
            .On(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter, () => { })
            .On(ModifierKeys.None, Key.Escape, () =>
            {
                if (!lists.Popups.CloseOpen())
                {
                    requestCancel();
                }
            });
        router.Listen(owner, editorKeys);
    }

    /// <summary>The field's text. Setting it replaces the text and starts <see cref="HasChanges"/> over.</summary>
    public string Value
    {
        get => _field.Text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _field.Text = value;
            _baseline = value;
        }
    }

    /// <summary>Whether the text differs at all from <see cref="Value"/> as last set.</summary>
    public bool HasChanges => _field.Text != _baseline;

    /// <summary>Calls <paramref name="save"/> with the text. Returns false, keeping the text and showing why, when save threw a failure Tasks' saves are known to throw.</summary>
    public bool Save(Action<string> save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return SaveFailures.Try(save, _field.Text, _message.Show);
    }
}
