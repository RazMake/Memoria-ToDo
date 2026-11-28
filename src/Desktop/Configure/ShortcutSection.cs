using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The content of Application's Shortcuts card: how to change a shortcut, and a row for Add new task, Task View,
/// Contacts, Configure, Close window, and Save. Each row's pick must differ from the five others. A focused field of any
/// row releases the four global shortcuts; Close window and Save have none to release.
/// </summary>
internal sealed class ShortcutSection : StackPanel
{
    private readonly ShortcutRow[] _rows;
    private readonly ShortcutFocus _focus;

    public ShortcutSection(
        ShortcutSetting addNewTask, ShortcutSetting manageTasks, ShortcutSetting contacts, ShortcutSetting configure,
        CloseWindowSetting closeWindow, SaveSetting save)
    {
        IShortcutSetting[] all = [addNewTask, manageTasks, contacts, configure, closeWindow, save];
        ShortcutRow Row(IShortcutSetting setting, string description, Action<ModifierKeys> swallowed) =>
            new(setting, description, [.. all.Where(other => other != setting)], swallowed);

        _rows =
        [
            Row(addNewTask, "Opens the Add window from any app.", addNewTask.KeySwallowed),
            Row(manageTasks, "Opens Task View from any app.", manageTasks.KeySwallowed),
            Row(contacts, "Opens Contacts from any app.", contacts.KeySwallowed),
            Row(configure, "Opens Configure from any app.", configure.KeySwallowed),
            Row(closeWindow, "Closes the app's window you're in, whatever has the focus.", _ => { }),
            Row(save, "Saves the task you're editing, in Add and Task View.", _ => { }),
        ];
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(140) }, new ColumnDefinition() },
        };
        for (var row = 0; row < _rows.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _rows[row].AddTo(grid, row);
        }

        Children.Add(new TextBlock
        {
            Text = "Click a box, then press the new shortcut. Esc keeps the current one:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });
        Children.Add(grid);

        _focus = new ShortcutFocus([addNewTask, manageTasks, contacts, configure], Refresh);
        foreach (var field in _rows.Select(row => row.Field))
        {
            field.GotKeyboardFocus += (_, _) => _focus.FieldFocused();
            field.LostKeyboardFocus += (_, e) => _focus.FocusLeft(toAnotherField: e.NewFocus is ShortcutField);
        }
    }

    /// <summary>Registers the shortcuts again if a field still holds them released.</summary>
    public void WindowClosed() => _focus.Restore();

    /// <summary>Shows each row's current shortcut and message.</summary>
    public void Refresh()
    {
        foreach (var row in _rows)
        {
            row.Refresh();
        }
    }
}
