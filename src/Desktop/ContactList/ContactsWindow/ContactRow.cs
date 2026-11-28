using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>
/// One contact's row in the list: the full name, then the alias in italics between brackets when there is one.
/// Found on screen by its automation id "Row".
/// </summary>
internal sealed class ContactRow : Border
{
    public ContactRow(ListedContact listed, ContextMenu? menu)
    {
        Listed = listed;
        Focusable = true;
        // Selection follows the focus, so the selected background already shows it; no focus frame.
        FocusVisualStyle = null;
        Padding = new Thickness(8, 4, 8, 4);
        Margin = new Thickness(16, 0, 0, 0);
        CornerRadius = new CornerRadius(4);
        ContextMenu = menu;
        AutomationProperties.SetAutomationId(this, "Row");
        AutomationProperties.SetName(this, listed.Contact.FullName);
        var text = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        text.Inlines.Add(listed.Contact.FullName);
        if (!string.IsNullOrEmpty(listed.Contact.Alias))
        {
            text.Inlines.Add(" (");
            text.Inlines.Add(new System.Windows.Documents.Italic(new System.Windows.Documents.Run(listed.Contact.Alias)));
            text.Inlines.Add(")");
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
