using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A list of strings shown as buttons under a field, only while open. A click, or Down then Enter, chooses an item and
/// raises <see cref="Chosen"/> with its position. It never takes the keyboard focus, so the field keeps it while the list
/// is open.
/// </summary>
internal sealed class PopupList : ListBox
{
    public PopupList(string automationId)
    {
        Focusable = false;
        Visibility = Visibility.Collapsed;
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
        AutomationProperties.SetAutomationId(this, automationId);
        ClickableItems.AttachByPosition(this, ClickableItems.ItemButton(), position => Chosen?.Invoke(this, position));
    }

    /// <summary>Raised with the position of the item the user chose.</summary>
    public event EventHandler<int>? Chosen;

    public bool IsOpen => Visibility == Visibility.Visible;

    /// <summary>Shows the items with none chosen yet.</summary>
    public void Show(IReadOnlyList<string> items)
    {
        ItemsSource = items;
        SelectedIndex = -1;
        Visibility = Visibility.Visible;
    }

    /// <summary>Hides the list. Returns false, doing nothing, when it was not shown.</summary>
    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        Visibility = Visibility.Collapsed;
        ItemsSource = null;
        return true;
    }

    /// <summary>Moves to the next item.</summary>
    public void MoveDown() => ListBoxes.ChooseNext(this);

    /// <summary>Chooses the item Down moved to. Returns false, doing nothing, when none was moved to.</summary>
    public bool ChooseMoved()
    {
        if (SelectedIndex < 0)
        {
            return false;
        }

        Chosen?.Invoke(this, SelectedIndex);
        return true;
    }
}
