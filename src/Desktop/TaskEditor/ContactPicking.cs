using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Rendering;
using Serilog;

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
    private readonly ILogger _logger;
    private TextAnchor? _at;
    private bool _closingForNoMatch;
    private IReadOnlyList<Contact> _shown = [];
    private IReadOnlyList<MentionRow> _rows = [];
    private IReadOnlyList<string> _items = [];

    protected ContactPicking(ITextField field, ChoiceList list, PopupLists popups, IContacts contacts, ILogger? logger = null)
    {
        Field = field;
        Contacts = contacts;
        _list = list;
        _popups = popups;
        _logger = logger ?? Serilog.Core.Logger.None;
        var otherListWasOpen = popups.WatchOtherTypedListOpenAtEdits(field, list);

        // An edit that only removes text may leave the cursor at the end of a typed query: see OpenAfterErase.
        var erased = false;
        field.Document.Changing += (_, e) => erased = e.InsertionLength == 0 && e.RemovalLength > 0;
        field.TextEntered += text =>
        {
            if (text == "@" && !otherListWasOpen())
            {
                Open();
            }
        };
        field.Changed += () =>
        {
            var erase = erased;
            erased = false;
            if (_at is not null)
            {
                Update(opening: false);
            }
            else if (erase)
            {
                OpenAfterErase();
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

    /// <summary>Called after a chosen contact's text is inserted; the list is closed and the field has the focus.</summary>
    protected virtual void Chosen(Contact contact)
    {
    }

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
            Start(at);
        }
    }

    // An edit that only removed text opens the list when it left the cursor where a typed query would end, as if the user had typed
    // it: at the "@" Editing finds before the cursor. Not while another list is open, nor when a mention lies between the "@" and
    // the cursor or holds the "@" (a chip ended that query; a mention shown as Markdown isn't a query).
    private void OpenAfterErase()
    {
        var current = Field.Current();
        if (_popups.IsOpen || current.SelectionLength != 0 || MentionPicker.AtBefore(current) is not { } at)
        {
            return;
        }

        var cursor = current.SelectionStart;
        if (MentionOverlaps(current.Text, at, cursor))
        {
            _logger.Debug("Erasing left the cursor after an \"@\" at {At}, but a mention lies on it, so the contact list stays closed", at);
            return;
        }

        _logger.Debug("Erasing left the cursor at the end of a {Length}-character query after the \"@\" at {At}, so the contact list opens", cursor - at - 1, at);
        Start(Field.CaretOffset - (cursor - at));
    }

    private static bool MentionOverlaps(string text, int at, int cursor) =>
        text.Contains("](", StringComparison.Ordinal)
        && Mentions.In(text, true).Any(mention => mention.Start < cursor && mention.Start + mention.Length > at);

    private void Start(int at)
    {
        _at = Field.AnchorAt(at, staysBefore: false);
        Opening();
        Update(opening: true);
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
        var contact = _shown[position];
        var text = TextFor(contact);
        End();
        var at = Field.LfOffset(offset);
        Field.Run(current => MentionPicker.Insert(current, at, text));
        Field.FocusField();
        Chosen(contact);
    }
}
