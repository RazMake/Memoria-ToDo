using System.Windows.Input;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What a key does to the selected row. <c>Edit</c> is Edit Description.</summary>
internal enum RowAction { Toggle, Edit, EditTitle, Resolve, Delete, MoveUp, MoveDown }

/// <summary>The keys that act on the selected row. A key acts only with exactly its modifiers.</summary>
internal static class RowKeys
{
    private static readonly Dictionary<(ModifierKeys, Key), RowAction> Actions = new()
    {
        [(ModifierKeys.None, Key.Enter)] = RowAction.Toggle,
        [(ModifierKeys.None, Key.F2)] = RowAction.Edit,
        [(ModifierKeys.Shift, Key.F2)] = RowAction.EditTitle,
        [(ModifierKeys.Control, Key.D)] = RowAction.Resolve,
        [(ModifierKeys.None, Key.Delete)] = RowAction.Delete,
        [(ModifierKeys.Alt, Key.Up)] = RowAction.MoveUp,
        [(ModifierKeys.Alt, Key.Down)] = RowAction.MoveDown,
    };

    public static RowAction? ActionFor(ModifierKeys modifiers, Key key) =>
        Actions.TryGetValue((modifiers, key), out var action) ? action : null;
}
