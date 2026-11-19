using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>One contact's row in the list: the full name, with the title after it. Found on screen by its automation id "Row".</summary>
internal sealed class ContactRow : Border
{
    public ContactRow(ListedContact listed, ContextMenu? menu)
    {
        Listed = listed;
        Focusable = true;
        Padding = new Thickness(8, 4, 8, 4);
        Margin = new Thickness(16, 0, 0, 0);
        CornerRadius = new CornerRadius(4);
        ContextMenu = menu;
        AutomationProperties.SetAutomationId(this, "Row");
        AutomationProperties.SetName(this, listed.Contact.FullName);
        var text = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        text.Inlines.Add(listed.Contact.FullName);
        if (!string.IsNullOrEmpty(listed.Title))
        {
            var title = new System.Windows.Documents.Run("  " + listed.Title);
            ContactHelpStyle.Apply(title);
            text.Inlines.Add(title);
        }

        Child = text;
        MarkSelected(false);
    }

    public ListedContact Listed { get; }

    public void MarkSelected(bool selected)
    {
        AutomationProperties.SetItemStatus(this, selected ? "Selected" : "");
        if (selected)
        {
            SetResourceReference(BackgroundProperty, "SubtleFillColorSecondaryBrush");
        }
        else
        {
            ClearValue(BackgroundProperty);
            Background = System.Windows.Media.Brushes.Transparent;
        }
    }
}
