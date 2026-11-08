using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Shortcuts section's content: how to change a shortcut, and a row for each shortcut.</summary>
internal sealed class ShortcutSection : StackPanel
{
    private readonly ShortcutRow[] _rows;
    private readonly ShortcutFocus _focus;

    public ShortcutSection(ShortcutSetting addNewTask, ShortcutSetting manageTasks)
    {
        _rows = [new ShortcutRow(addNewTask, manageTasks), new ShortcutRow(manageTasks, addNewTask)];
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(140) }, new ColumnDefinition() },
        };
        for (var row = 0; row < _rows.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _rows[row].AddTo(grid, row);
        }

        Children.Add(ThemedText.Help(
            "Click a box, then press the new shortcut. Esc keeps the current one.", new Thickness(0, 0, 0, 8)));
        Children.Add(grid);

        _focus = new ShortcutFocus([addNewTask, manageTasks], _rows.Select(row => row.Field), RefreshRows);
    }

    /// <summary>Registers the shortcuts again if a field still holds them released.</summary>
    public void WindowClosed() => _focus.Restore();

    private void RefreshRows()
    {
        foreach (var row in _rows)
        {
            row.Refresh();
        }
    }
}
