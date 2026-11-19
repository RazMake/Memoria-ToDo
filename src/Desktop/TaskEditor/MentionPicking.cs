using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Typing "@" opens the contact list. It narrows as the user types, shows each contact in the current template, moves to the
/// next template on Tab, and choosing a contact inserts its mention in place of the "@" and what was typed after it.
/// A list shared by several fields belongs to the field that showed its items last.
/// </summary>
internal sealed class MentionPicking
{
    /// <summary>The most contacts the list shows.</summary>
    public const int MostShown = 7;

    private readonly ITextField _field;
    private readonly ChoiceList _list;
    private readonly PopupLists _popups;
    private readonly IContacts _contacts;
    private TextAnchor? _at;
    private bool _closingForNoMatch;
    private int _template;
    private string _templateText = "";
    private IReadOnlyList<Contact> _shown = [];
    private IReadOnlyList<string> _items = [];
    private IReadOnlyList<string?> _titles = [];

    public MentionPicking(ITextField field, ChoiceList list, PopupLists popups, IContacts contacts)
    {
        _field = field;
        _list = list;
        _popups = popups;
        _contacts = contacts;
        var otherListWasOpen = popups.WatchOtherTypedListOpenAtEdits(field, list);
        field.TextEntered += text =>
        {
            if (text == "@" && !otherListWasOpen())
            {
                Open();
            }
        };
        field.Changed += () =>
        {
            if (_at is not null)
            {
                Update(opening: false);
            }
        };

        // The list closing from outside, as Esc or another list opening closes it, ends the typing of a mention.
        list.Changed += () =>
        {
            if (!list.IsOpen && !_closingForNoMatch)
            {
                _at = null;
            }
        };
        list.Chosen += Choose;
    }

    /// <summary>The field this picking opens the list for.</summary>
    public ITextField Field => _field;

    /// <summary>Whether this field's contacts are the ones the list shows now.</summary>
    public bool IsShowing => _list.IsOpen && ReferenceEquals(_list.Items, _items);

    /// <summary>Each row's contact title, in the help style after its text; null for a contact with none. Parallel to the list's items.</summary>
    public IReadOnlyList<string?> Titles => IsShowing ? _titles : [];

    /// <summary>The list's last line: the current template and "Tab: next template".</summary>
    public string Hint => IsShowing ? $"{_templateText}   Tab: next template" : "";

    /// <summary>Tab: moves to the next template. Returns true when the list is open, so the key stays in the field.</summary>
    public bool NextTemplate()
    {
        if (!IsShowing)
        {
            return false;
        }

        _template++;
        Update(opening: false);
        return true;
    }

    private void Open()
    {
        var at = _field.CaretOffset - 1;
        if (at >= 0 && MentionPicker.OpensAt(_field.Current().Text, _field.LfOffset(at)))
        {
            _at = _field.AnchorAt(at, staysBefore: false);
            _template = 0;
            Update(opening: true);
        }
    }

    // Lists the contacts that match, or closes the list when none do (keeping the "@" in case erasing brings a match back)
    // or when the "@" no longer opens the picker. Gives up the list when another field has taken it.
    private void Update(bool opening)
    {
        if (_list.IsOpen && !IsShowing && !opening)
        {
            _at = null;
            return;
        }

        if (_at is not { IsDeleted: false } at
            || MentionPicker.QueryOf(_field.Current(), _field.LfOffset(at.Offset)) is not { } query)
        {
            End();
            return;
        }

        _shown = [.. _contacts.Search(query).Take(MostShown)];
        if (_shown.Count == 0)
        {
            _closingForNoMatch = true;
            _list.Close();
            _closingForNoMatch = false;
            return;
        }

        var templates = _contacts.MentionTemplates;
        _templateText = templates[_template % templates.Count];
        _titles = [.. _shown.Select(contact => string.IsNullOrEmpty(contact.Title) ? null : contact.Title)];
        _items = [.. _shown.Select(contact => _contacts.TextOf(contact, _templateText))];
        if (_list.IsOpen)
        {
            _list.Show(_items);
        }
        else
        {
            _popups.Show(_list, _items);
        }
    }

    private void End()
    {
        _at = null;
        _list.Close();
    }

    private void Choose(int position)
    {
        if (!IsShowing)
        {
            return;
        }

        var contact = _shown[position];
        var offset = _at?.Offset ?? throw new InvalidOperationException("A contact was chosen with no \"@\" typed.");
        var mention = _contacts.MentionOf(contact, _templateText);
        End();
        var at = _field.LfOffset(offset);
        _field.Run(current => MentionPicker.Insert(current, at, mention));
        _field.FocusField();
    }
}
