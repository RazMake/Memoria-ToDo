using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>One shortcut setting as the window shows it: its name, its field, and under the field the message about it.</summary>
internal sealed class ShortcutRow
{
    private readonly ShortcutSetting _setting;
    private readonly TextBlock _message = ThemedText.Message();

    public ShortcutRow(ShortcutSetting setting, ShortcutSetting other)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(other);
        _setting = setting;
        Field = new ShortcutField(
            (modifiers, key) => Changed(() => setting.Pick(modifiers, key, other)),
            modifiers => Changed(() => setting.KeySwallowed(modifiers)))
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Refresh();
    }

    /// <summary>The row's shortcut field.</summary>
    public ShortcutField Field { get; }

    /// <summary>Puts the row in a two-column grid: the name in the first column, the field and its message in the second.</summary>
    public void AddTo(Grid grid, int row)
    {
        var name = new TextBlock { Text = _setting.Label, Margin = new Thickness(0, 6, 12, 0) };
        var fieldAndMessage = new StackPanel { Margin = new Thickness(0, 0, 0, 5), Children = { Field, _message } };
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
