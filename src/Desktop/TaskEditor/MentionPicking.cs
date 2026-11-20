using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The "@" list of a field that holds mentions: its header shows the contact moved to in the current mention template, Tab or a
/// click on the header moves to the next template, and choosing a contact inserts its mention in that template.
/// </summary>
internal sealed class MentionPicking(ITextField field, ChoiceList list, PopupLists popups, IContacts contacts)
    : ContactPicking(field, list, popups, contacts)
{
    private int _template;

    /// <summary>The contact moved to in the current template, or the first row's contact while none is moved to.</summary>
    public override string Header => IsShowing ? Contacts.TextOf(Highlighted, CurrentTemplate) : "";

    /// <summary>"Tab or click: next template" while there are two or more templates.</summary>
    public override string TemplateHint => IsShowing && Contacts.MentionTemplates.Count > 1 ? "Tab or click: next template" : "";

    /// <summary>Moves to the next template. Returns true when the list is open, so the key stays in the field.</summary>
    public bool NextTemplate()
    {
        if (!IsShowing)
        {
            return false;
        }

        _template++;
        List.Refresh();
        return true;
    }

    protected override void Opening() => _template = 0;

    protected override string TextFor(Contact contact) => Contacts.MentionOf(contact, CurrentTemplate);

    private string CurrentTemplate
    {
        get
        {
            var templates = Contacts.MentionTemplates;
            return templates[_template % templates.Count];
        }
    }
}
