using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Moving through a list with the keyboard.</summary>
internal static class ListBoxes
{
    /// <summary>Chooses the item after the chosen one, or the first when none is chosen; stays on the last.</summary>
    public static void ChooseNext(this ListBox list) =>
        list.SelectedIndex = Math.Min(list.SelectedIndex + 1, list.Items.Count - 1);
}
