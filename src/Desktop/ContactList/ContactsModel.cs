using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>The questions the Contacts window asks the user: <see cref="ILeavePrompt.Ask"/> for a form with changes, and the delete question.</summary>
internal interface IContactPrompts : ILeavePrompt
{
    /// <summary>Asks "Delete {fullName}?" with Delete and Cancel, Cancel the default. True for Delete.</summary>
    bool ConfirmDelete(string fullName);
}

/// <summary>A contact's place: its group's file name and its ID.</summary>
internal sealed record ContactRef(string GroupName, string Id);

/// <summary>One row of the list: the contact, and its title shown after the full name.</summary>
internal sealed record ListedContact(string GroupName, Contact Contact, string? Title);

/// <summary>One group of the list: its heading, whether Add shows on it, and the contacts it shows.</summary>
internal sealed record ListedGroup(string Name, string Heading, bool CanAdd, IReadOnlyList<ListedContact> Contacts);

/// <summary>
/// What the Contacts window shows and does, with no screen: the list and its search, the selection, the details, the form,
/// the messages, and the questions it asks. The window calls <see cref="Refresh"/> on the UI thread after Contacts' Changed.
/// </summary>
internal sealed class ContactsModel
{
    private readonly IContacts _contacts;
    private readonly IContactPrompts _prompts;
    private readonly LeaveDecision _leave;
    private string _search = "";
    private IReadOnlyList<ContactGroup> _loaded = [];
    private IReadOnlyList<ListedGroup> _groups = [];
    private string? _listText;
    private ContactRef? _selectedRef;
    private ListedContact? _selected;
    private ContactDetails? _details;
    private ContactForm? _form;
    private string? _message;
    private ContactProblems _problems = new(null, []);
    private bool _isOff;

    public ContactsModel(IContacts contacts, IContactPrompts prompts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(prompts);
        _contacts = contacts;
        _prompts = prompts;
        _leave = new LeaveDecision(prompts);
        Rebuild();
    }

    /// <summary>The words typed in the search band. Setting it filters the list through Contacts' search, and clears the selection when it no longer shows.</summary>
    public string SearchText
    {
        get => _search;
        set
        {
            _search = value ?? "";
            Rebuild();
        }
    }

    /// <summary>The groups the list shows, in Contacts' order; a group with no match is left out while searching, an empty group shows otherwise.</summary>
    public IReadOnlyList<ListedGroup> Groups => _groups;

    /// <summary>The text that replaces the list when it shows no group, or null.</summary>
    public string? ListText => _listText;

    /// <summary>The selected contact, or null.</summary>
    public ListedContact? Selected => _selected;

    /// <summary>The selected contact's details, or null when nothing is selected.</summary>
    public ContactDetails? Details => _details;

    /// <summary>What the right side says while it has neither details nor a form, or null.</summary>
    public string? DetailsText => _selected is null && _form is null ? "Pick a contact to see the details." : null;

    /// <summary>The open form, or null.</summary>
    public ContactForm? Form => _form;

    /// <summary>Why the last action failed, or null. The next action the user starts clears it.</summary>
    public string? Message => _message;

    /// <summary>The line about skipped files and shared names.</summary>
    public ContactProblems Problems => _problems;

    /// <summary>Whether Add, Edit, Delete, and the drag are off since there are no contact files or no clone.</summary>
    public bool IsOff => _isOff;

    public bool CanEdit => _selected is not null && !_isOff && _form is null;

    /// <summary>Whether Delete is offered: a contact is selected, it isn't the user's own, no form is open, and contacts exist.</summary>
    public bool CanDelete => CanEdit && CanDeleteFrom(_selected!.GroupName);

    /// <summary>Whether a new contact can be added to the group: contacts exist, and it isn't the user's own group.</summary>
    public bool CanAddTo(string groupName) => !_isOff && !ContactOwnGroup.Is(groupName);

    /// <summary>Whether the group's contacts can be deleted, whatever is selected: contacts exist, and it isn't the user's own group.</summary>
    public bool CanDeleteFrom(string groupName) => !_isOff && !ContactOwnGroup.Is(groupName);

    /// <summary>Whether a group heading can be dragged: no search is typed, no form is open, and contacts exist.</summary>
    public bool CanReorder => !_isOff && string.IsNullOrWhiteSpace(_search) && _form is null;

    /// <summary>Reads Contacts again, keeping the selection and the open form.</summary>
    public void Refresh() => Rebuild();

    /// <summary>Selects the contact; asks first when the form has changes. False when the selection stays.</summary>
    public bool Select(string groupName, string id)
    {
        if (!Exists(groupName, id))
        {
            return false;
        }

        _message = null;
        if (_form is { IsNew: false } form && form.GroupName == groupName && form["id"] == id)
        {
            return true;
        }

        if (!MayLeaveForm())
        {
            return false;
        }

        Show(new ContactRef(groupName, id));
        return true;
    }

    /// <summary>Opens the form on the selected contact. False when nothing is selected or the user cancels.</summary>
    public bool BeginEdit()
    {
        _message = null;
        if (_form is not null || !CanEdit)
        {
            return false;
        }

        _form = ContactForm.Edit(_contacts, _selected!.GroupName, _selected.Contact);
        return true;
    }

    /// <summary>Opens an empty form for the group; asks first when a form has changes. False when Add is off for the group or the user cancels.</summary>
    public bool BeginAdd(string groupName)
    {
        _message = null;
        if (!CanAddTo(groupName) || _loaded.All(group => group.Name != groupName) || !MayLeaveForm())
        {
            return false;
        }

        ContactForm? created = null;
        if (!Run(() => created = ContactForm.Add(_contacts, groupName), groupName))
        {
            return false;
        }

        _form = created;
        return true;
    }

    /// <summary>Leaves the form without saving (Esc), asking when it has changes. False when the form stays.</summary>
    public bool LeaveForm()
    {
        _message = null;
        return MayLeaveForm();
    }

    /// <summary>Saves the form through Contacts. On success leaves the form and selects the saved contact. False when the form stays.</summary>
    public bool SaveForm()
    {
        _message = null;
        if (_form is not { HasProblems: false } form)
        {
            return false;
        }

        var contact = form.ToContact();
        if (!Run(() => _contacts.Save(form.GroupName, contact), form.GroupName))
        {
            return false;
        }

        _form = null;
        Show(new ContactRef(form.GroupName, contact.Id));
        return true;
    }

    /// <summary>Asks, then deletes the selected contact. False when it can't be offered, the user cancels, or it fails.</summary>
    public bool Delete()
    {
        _message = null;
        if (!CanDelete || !_prompts.ConfirmDelete(_selected!.Contact.FullName))
        {
            return false;
        }

        var (group, id) = (_selected.GroupName, _selected.Contact.Id);
        if (!Run(() => _contacts.Delete(group, id), group))
        {
            return false;
        }

        _selectedRef = null;
        Rebuild();
        return true;
    }

    /// <summary>Moves the group before the other group, or to the end when <paramref name="beforeGroup"/> is null. False when the drag is off or it fails.</summary>
    public bool MoveGroup(string groupName, string? beforeGroup)
    {
        _message = null;
        if (!CanReorder)
        {
            return false;
        }

        if (beforeGroup == groupName)
        {
            return true;
        }

        var others = _loaded.Select(group => group.Name).Where(name => name != groupName).ToList();
        var index = beforeGroup is null ? others.Count : others.IndexOf(beforeGroup);
        var previous = index > 0 ? others[index - 1] : null;
        string[] names = beforeGroup is null ? [groupName] : [groupName, beforeGroup];
        if (!Run(() => _contacts.MoveGroup(groupName, previous, beforeGroup), names))
        {
            return false;
        }

        Rebuild();
        return true;
    }

    /// <summary>Whether the window may close: asks about a form with changes. False when cancelled, the save fails, or the question already shows.</summary>
    public bool MayClose() => MayLeaveForm();

    private bool Run(Action call, params string[] groups) =>
        StoreFailures.Run(call, failure => ContactFailures.MessageOf(failure, _contacts, groups), message => _message = message);

    private bool MayLeaveForm()
    {
        if (!_leave.MayLeave(_form is null ? null : new LeavingForm(this)))
        {
            return false;
        }

        _form = null;
        return true;
    }

    private bool Exists(string groupName, string id) =>
        _loaded.Any(group => group.Name == groupName && group.Members.Any(member => member.Id == id));

    // Selects the contact, emptying the search when it would hide the contact.
    private void Show(ContactRef contact)
    {
        _selectedRef = contact;
        if (!Rebuild(keepHidden: true))
        {
            _search = "";
            Rebuild();
        }
    }

    // Reads Contacts and rebuilds what the window shows. False when the selected contact is kept though the search hides it.
    private bool Rebuild(bool keepHidden = false)
    {
        _loaded = _contacts.Groups;
        _isOff = !_contacts.HasContactFiles;
        _problems = ContactProblems.Of(_contacts);

        var searching = !string.IsNullOrWhiteSpace(_search);
        HashSet<Contact>? matches = searching ? [.. _contacts.Search(_search)] : null;
        _groups =
        [
            .. _loaded
                .Select(group => new ListedGroup(
                    group.Name,
                    group.Heading,
                    CanAddTo(group.Name),
                    [.. group.Members.Where(member => matches is null || matches.Contains(member)).Select(member => new ListedContact(group.Name, member, member.Title))]))
                .Where(group => !searching || group.Contacts.Count > 0),
        ];
        _listText = _isOff ? "There are no contact files in this repository."
            : _loaded.Count == 0 ? "No contact file could be read. See the problems line."
            : _groups.Count == 0 ? "No contact matches the search."
            : null;

        var visible = true;
        _selected = null;
        if (_selectedRef is { } selected)
        {
            _selected = _groups.Where(group => group.Name == selected.GroupName).SelectMany(group => group.Contacts)
                .FirstOrDefault(row => row.Contact.Id == selected.Id);
            if (_selected is null && keepHidden)
            {
                var group = _loaded.FirstOrDefault(g => g.Name == selected.GroupName);
                var member = group?.Members.FirstOrDefault(m => m.Id == selected.Id);
                _selected = member is null ? null : new ListedContact(selected.GroupName, member, member.Title);
                visible = _selected is null;
            }

            if (_selected is null)
            {
                _selectedRef = null;
            }
        }

        _details = _selected is null
            ? null
            : ContactDetailsBuilder.Of(_contacts, _loaded.First(group => group.Name == _selected.GroupName), _selected.Contact);
        return visible;
    }

    /// <summary>The open form as the changed edit <see cref="LeaveDecision"/> asks about.</summary>
    private sealed class LeavingForm(ContactsModel model) : ILeavingEdit
    {
        public bool HasChanges => model._form!.HasChanges;

        public bool Save() => model.SaveForm();

        public void Discard() => model._form = null;

        public void ReturnToEditor()
        {
        }
    }
}
