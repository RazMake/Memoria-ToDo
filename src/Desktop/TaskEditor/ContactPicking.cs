using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Typing "@" opens the contact list. It narrows as the user types, shows each contact as a <see cref="MentionRow"/>, and choosing
/// a contact replaces the "@" and what was typed after it with the text the kind of picking makes. The list closes when Editing's
/// query answers nothing, when nothing matches, when the field loses the keyboard focus, and when another list opens.
/// A list shared by several fields belongs to the field that showed its items last.
/// </summary>
internal abstract class ContactPicking
{
    /// <summary>The most contacts the list shows.</summary>
    public const int MostShown = 7;

    private readonly ChoiceList _list;
    private readonly PopupLists _popups;
    private TextAnchor? _at;
    private bool _closingForNoMatch;
    private IReadOnlyList<Contact> _shown = [];
    private IReadOnlyList<MentionRow> _rows = [];
    private IReadOnlyList<string> _items = [];

    protected ContactPicking(ITextField field, ChoiceList list, PopupLists popups, IContacts contacts)
    {
        Field = field;
        Contacts = contacts;
        _list = list;
        _popups = popups;
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
        field.FocusLost += () =>
        {
            if (IsShowing)
            {
                End();
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
    public ITextField Field { get; }

    /// <summary>Whether this field's contacts are the ones the list shows now.</summary>
    public bool IsShowing => _list.IsOpen && ReferenceEquals(_list.Items, _items);

    /// <summary>The rows, parallel to the list's items; empty while this field's list is closed.</summary>
    public IReadOnlyList<MentionRow> Rows => IsShowing ? _rows : [];

    /// <summary>The place of the typed "@" in the field's text, which follows edits; null while no "@" is being typed.</summary>
    public int? AtOffset => _at is { IsDeleted: false } at ? at.Offset : null;

    /// <summary>The line at the top of the list; empty for a list with none.</summary>
    public virtual string Header => "";

    /// <summary>The help line under the header; empty for a list with none.</summary>
    public virtual string TemplateHint => "";

    protected IContacts Contacts { get; }

    /// <summary>The list this picking shows its contacts in.</summary>
    protected ChoiceList List => _list;

    /// <summary>The contact the list's first row holds while none is moved to, else the one moved to. Only while <see cref="IsShowing"/>.</summary>
    protected Contact Highlighted => _shown[Math.Max(_list.SelectedIndex, 0)];

    /// <summary>Called when a typed "@" opens the list, before it shows.</summary>
    protected virtual void Opening()
    {
    }

    /// <summary>The text that replaces the "@" and what was typed after it when the contact is chosen.</summary>
    protected abstract string TextFor(Contact contact);

    /// <summary>Ends the typing of the "@" and closes the list.</summary>
    protected void End()
    {
        _at = null;
        _list.Close();
    }

    private void Open()
    {
        var at = Field.CaretOffset - 1;
        if (at >= 0 && MentionPicker.OpensAt(Field.Current().Text, Field.LfOffset(at)))
        {
            _at = Field.AnchorAt(at, staysBefore: false);
            Opening();
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
            || MentionPicker.QueryOf(Field.Current(), Field.LfOffset(at.Offset)) is not { } query)
        {
            End();
            return;
        }

        _shown = [.. Contacts.Search(query).Take(MostShown)];
        if (_shown.Count == 0)
        {
            _closingForNoMatch = true;
            _list.Close();
            _closingForNoMatch = false;
            return;
        }

        _rows = [.. _shown.Select(MentionRow.Of)];
        _items = [.. _rows.Select(row => row.Text)];
        if (_list.IsOpen)
        {
            _list.Show(_items);
        }
        else
        {
            _popups.Show(_list, _items);
        }
    }

    private void Choose(int position)
    {
        if (!IsShowing)
        {
            return;
        }

        var offset = _at?.Offset ?? throw new InvalidOperationException("A contact was chosen with no \"@\" typed.");
        var text = TextFor(_shown[position]);
        End();
        var at = Field.LfOffset(offset);
        Field.Run(current => MentionPicker.Insert(current, at, text));
        Field.FocusField();
    }
}
