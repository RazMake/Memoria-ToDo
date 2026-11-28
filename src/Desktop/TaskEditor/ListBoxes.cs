using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Showing a <see cref="ChoiceList"/> in a list box.</summary>
internal static class ListBoxes
{
    /// <summary>Makes the list box show the list's items and chosen item, and be visible only while the list is open.</summary>
    public static void ShowChoices(this ListBox box, ChoiceList list)
    {
        void Mirror()
        {
            if (!ReferenceEquals(box.ItemsSource, list.Items))
            {
                box.ItemsSource = list.Items;
            }

            box.SelectedIndex = list.SelectedIndex;
            box.Visibility = list.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        list.Changed += Mirror;
        Mirror();
    }
}
