using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The "@" list of a plain text box: no header and no templates, and choosing a contact leaves "@" and the contact's alias,
/// or its ID when it has none, in place of what was typed.
/// </summary>
internal sealed class ContactAliasPicking(ITextField field, ChoiceList list, PopupLists popups, IContacts contacts)
    : ContactPicking(field, list, popups, contacts)
{
    /// <summary>Tab: closes the list. Returns false, so the key moves the focus as it would with no list.</summary>
    public bool Tab()
    {
        if (IsShowing)
        {
            End();
        }

        return false;
    }

    protected override string TextFor(Contact contact) => "@" + MentionRow.Of(contact).Alias;
}
