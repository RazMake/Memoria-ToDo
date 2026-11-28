using System.Windows.Automation;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A <see cref="ChoiceList"/> shown as buttons under a field, only while open. A click chooses an item. It never takes
/// the keyboard focus, so the field keeps it while the list is open.
/// </summary>
internal sealed class PopupList : ListBox
{
    public PopupList(string automationId, ChoiceList list)
    {
        Focusable = false;
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
        AutomationProperties.SetAutomationId(this, automationId);
        ClickableItems.AttachByPosition(this, ClickableItems.ItemButton(), list.Choose);
        this.ShowChoices(list);
    }
}
