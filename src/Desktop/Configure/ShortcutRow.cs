using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// One shortcut setting as the window shows it: its name, its field, and under the field what the shortcut does, then
/// the message about it. Esc in the field keeps the shortcut and moves the focus out of the fields.
/// </summary>
internal sealed class ShortcutRow
{
    private readonly IShortcutSetting _setting;
    private readonly string _description;
    private readonly TextBlock _message = ThemedText.Message();

    /// <param name="setting">The row's setting.</param>
    /// <param name="description">What the shortcut does.</param>
    /// <param name="others">The other rows' shortcuts, which a pick must differ from.</param>
    /// <param name="swallowed">Called with the modifiers of a chord released with no other key.</param>
    public ShortcutRow(IShortcutSetting setting, string description, IUsedShortcut[] others, Action<ModifierKeys> swallowed)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(others);
        ArgumentNullException.ThrowIfNull(swallowed);
        _setting = setting;
        _description = description;
        Field = new ShortcutField(
            (modifiers, key) => Changed(() => setting.Pick(modifiers, key, others)),
            modifiers => Changed(() => swallowed(modifiers)),
            Keyboard.ClearFocus)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Refresh();
    }

    /// <summary>The row's shortcut field.</summary>
    public ShortcutField Field { get; }

    /// <summary>Puts the row in a two-column grid: the name in the first column, the field, its description, and its message in the second.</summary>
    public void AddTo(Grid grid, int row)
    {
        var name = new TextBlock { Text = _setting.Label, Margin = new Thickness(0, 6, 12, 0) };
        var description = ThemedText.Help(_description, new Thickness(0, 2, 0, 0));
        description.FontStyle = FontStyles.Italic;
        var fieldAndMessage = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 5),
            Children = { Field, description, _message },
        };
        Grid.SetRow(name, row);
        Grid.SetRow(fieldAndMessage, row);
        Grid.SetColumn(fieldAndMessage, 1);
        grid.Children.Add(name);
        grid.Children.Add(fieldAndMessage);
    }

    /// <summary>Shows the setting's shortcut and message.</summary>
    public void Refresh()
    {
        Field.Show(_setting.Active);
        ThemedText.ShowMessage(_message, _setting.Message);
    }

    private void Changed(Action change)
    {
        change();
        Refresh();
    }
}
