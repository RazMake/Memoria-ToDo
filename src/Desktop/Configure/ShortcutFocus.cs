using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Releases the shortcuts while any shortcut field has the keyboard focus, so their keys reach the field, and
/// registers them again when the focus leaves the fields. Moving from one field to the other keeps them released.
/// </summary>
internal sealed class ShortcutFocus
{
    private readonly IReadOnlyList<ShortcutSetting> _settings;
    private readonly Action _changed;
    private bool _released;

    /// <param name="settings">The shortcuts to release and register again.</param>
    /// <param name="fields">The fields whose focus releases the shortcuts.</param>
    /// <param name="changed">Called after the settings changed, so the window shows them.</param>
    public ShortcutFocus(IReadOnlyList<ShortcutSetting> settings, IEnumerable<ShortcutField> fields, Action changed)
    {
        _settings = settings;
        _changed = changed;
        foreach (var field in fields)
        {
            field.GotKeyboardFocus += (_, _) => Release();
            field.LostKeyboardFocus += OnLostKeyboardFocus;
        }
    }

    /// <summary>Registers the shortcuts again when a field still holds them released, as when the window closes.</summary>
    public void Restore()
    {
        if (!_released)
        {
            return;
        }

        _released = false;
        foreach (var setting in _settings)
        {
            setting.Restore();
        }

        _changed();
    }

    private void Release()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        foreach (var setting in _settings)
        {
            setting.Release();
        }

        _changed();
    }

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not ShortcutField)
        {
            Restore();
        }
    }
}
