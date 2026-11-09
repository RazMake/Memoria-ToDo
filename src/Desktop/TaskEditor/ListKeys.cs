using System.Windows.Input;
using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Enter, Tab, and Shift+Tab in the Description: they run Editing's list commands in a list item and keep their usual meaning elsewhere.</summary>
internal static class ListKeys
{
    /// <summary>
    /// Registers the keys. Tab outside a list item runs <paramref name="leaveForward"/> and Shift+Tab outside one runs
    /// <paramref name="leaveBack"/>; in a list item they only move the item, even when it can't move.
    /// </summary>
    public static void AddTo(KeyMap keys, StyledTextEditor description, Action leaveForward, Action leaveBack) =>
        keys
            .Try(ModifierKeys.None, Key.Enter, () => description.Run(Lists.Enter))
            .Try(ModifierKeys.None, Key.Tab, () => description.Run(Lists.Indent) || Left(leaveForward))
            .Try(ModifierKeys.Shift, Key.Tab, () => description.Run(Lists.Outdent) || Left(leaveBack));

    private static bool Left(Action leave)
    {
        leave();
        return true;
    }
}
