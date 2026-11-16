using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Shortcuts section's content: how to change a shortcut, and a row for Add new task, Task View, Close
/// window, and Save. A focused field of any row releases the two global shortcuts; Close window and Save have none to release.
/// </summary>
internal sealed class ShortcutSection : StackPanel
{
    private readonly ShortcutRow[] _rows;
    private readonly ShortcutFocus _focus;

    public ShortcutSection(
        ShortcutSetting addNewTask, ShortcutSetting manageTasks, CloseWindowSetting closeWindow, SaveSetting save)
    {
        _rows =
        [
            new ShortcutRow(addNewTask, "Opens the Add window from any app.", [manageTasks, closeWindow, save], addNewTask.KeySwallowed),
            new ShortcutRow(manageTasks, "Opens Task View from any app.", [addNewTask, closeWindow, save], manageTasks.KeySwallowed),
            new ShortcutRow(closeWindow, "Closes the app's window you're in, whatever has the focus.", [addNewTask, manageTasks, save], _ => { }),
            new ShortcutRow(save, "Saves the task you're editing, in Add and Task View.", [addNewTask, manageTasks, closeWindow], _ => { }),
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

        _focus = new ShortcutFocus([addNewTask, manageTasks], Refresh);
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
