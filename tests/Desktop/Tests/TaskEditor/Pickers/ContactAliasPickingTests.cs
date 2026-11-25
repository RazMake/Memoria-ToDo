using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.Pickers;

/// <summary>
/// The "@" list of a plain text box: typing "@" where Editing's mention list opens lists the contacts Contacts' search finds, and
/// choosing one replaces the "@" and what was typed after it with "@" and the contact's alias, or its ID when it has none. It has no
/// header and no templates. Where an "@" opens the list is Editing's rule, which contacts match is Contacts'. Tested over the real
/// Contacts, with the files seeded as literal JSON, and a text field that stands for the box.
/// </summary>
[Unit]
public class ContactAliasPickingTests
{
    private readonly EditorServices _services = new();
    private readonly ChoiceList _list = new();
    private readonly ChoiceList _other = new();
    private readonly PopupLists _popups;
    private readonly FakeTextField _field = new();
    private readonly ContactAliasPicking _picking;

    public ContactAliasPickingTests()
    {
        _popups = new PopupLists(_other, _list);
        _picking = new ContactAliasPicking(_field, _list, _popups, _services.Contacts);
    }

    private void UseContacts() =>
        _services.UseContacts(["{fullName} ({title})", "{firstName}"],
            ContactFiles.Person("ana", "Ana Pop", ("alias", "anap"), ("title", "Engineer")),
            ContactFiles.Person("bob", "Bob Stan"),
            ContactFiles.Person("cris", "Cris Ilie", ("alias", "ci")),
            ContactFiles.Person("dan", "Dan Rus"),
            ContactFiles.Person("eva", "Eva Mic"),
            ContactFiles.Person("fay", "Fay Nan"),
            ContactFiles.Person("gus", "Gus Oro"),
            ContactFiles.Person("hal", "Hal Pax"),
            ContactFiles.Person("ira", "Ira Qui"));

    private IEnumerable<string> FullNames => _picking.Rows.Select(row => row.FullName);

    [Fact]
    public void Typing_an_at_sign_lists_the_first_seven_contacts_by_full_name_and_leaves_the_at_sign()
    {
        UseContacts();

        _field.Type("@");

        Assert.True(_list.IsOpen);
        Assert.Equal(["Ana Pop", "Bob Stan", "Cris Ilie", "Dan Rus", "Eva Mic", "Fay Nan", "Gus Oro"], FullNames);
        Assert.Equal("@", _field.Text);
    }

    [Fact]
    public void Each_row_has_the_full_name_and_the_alias_or_the_ID_when_there_is_no_alias()
    {
        UseContacts();

        _field.Type("@");

        Assert.Equal(new MentionRow("Ana Pop", "anap"), _picking.Rows[0]);
        Assert.Equal(new MentionRow("Bob Stan", "bob"), _picking.Rows[1]);
        Assert.Equal(["Ana Pop (anap)", "Bob Stan (bob)"], _list.Items.Take(2));
    }

    [Fact]
    public void Typing_after_the_at_sign_narrows_the_list_by_Contacts_search()
    {
        UseContacts();

        _field.Type("@ANA p");

        Assert.Equal(["Ana Pop"], FullNames);
    }

    [Fact]
    public void It_opens_only_when_the_at_sign_is_typed_so_moving_the_cursor_into_a_word_opens_nothing()
    {
        UseContacts();
        var field = new FakeTextField("hi @anap", 8);
        _ = new ContactAliasPicking(field, _list, _popups, _services.Contacts);

        field.Cursor(6);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void An_erase_that_leaves_the_cursor_at_the_end_of_a_partial_word_opens_the_list_for_it()
    {
        UseContacts();
        var field = new FakeTextField("hi @anap", 8);
        _ = new ContactAliasPicking(field, _list, _popups, _services.Contacts);
        Assert.False(_list.IsOpen);

        field.Backspace();

        Assert.Equal("hi @ana", field.Text);
        Assert.Equal(["Ana Pop (anap)"], _list.Items);
    }

    [Fact]
    public void Erasing_back_to_a_bare_at_sign_opens_the_list_and_erasing_the_at_sign_itself_opens_nothing()
    {
        UseContacts();
        var field = new FakeTextField("hi @ana", 7);
        _ = new ContactAliasPicking(field, _list, _popups, _services.Contacts);

        field.Erase(4, 3);
        Assert.Equal(7, _list.Items.Count());

        _popups.CloseOpen();
        field.Backspace();

        Assert.Equal("hi ", field.Text);
        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void An_at_sign_after_a_letter_opens_no_list()
    {
        UseContacts();

        _field.Type("a@");

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void An_at_sign_after_a_space_opens_the_list_too()
    {
        UseContacts();

        _field.Type("find @");

        Assert.True(_list.IsOpen);
    }

    [Fact]
    public void With_no_repository_or_no_contacts_no_list_opens_and_the_at_sign_stays()
    {
        _field.Type("@");
        Assert.False(_list.IsOpen);

        _services.UseContacts(["{fullName}"]);
        _field.Type(" @");

        Assert.False(_list.IsOpen);
        Assert.Equal("@ @", _field.Text);
    }

    [Fact]
    public void The_list_closes_when_no_contact_matches_and_the_typed_text_stays()
    {
        UseContacts();
        _field.Type("@");

        _field.Type("zzz");

        Assert.False(_list.IsOpen);
        Assert.Equal("@zzz", _field.Text);
    }

    [Fact]
    public void Erasing_back_to_a_match_opens_the_list_again()
    {
        UseContacts();
        _field.Type("@bx");

        _field.Backspace();

        Assert.Equal(["Bob Stan"], FullNames);
    }

    [Fact]
    public void The_list_closes_when_the_at_sign_is_erased()
    {
        UseContacts();
        _field.Type("@a");

        _field.Backspace();
        _field.Backspace();

        Assert.False(_list.IsOpen);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void The_list_closes_when_the_cursor_moves_to_or_before_the_at_sign(int cursor)
    {
        UseContacts();
        _field.Type("ab @a");
        Assert.True(_list.IsOpen);

        _field.Cursor(cursor);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void The_list_closes_when_a_line_break_is_typed_after_the_at_sign()
    {
        UseContacts();
        _field.Type("@a");

        _field.Type("\n");

        Assert.False(_list.IsOpen);
        Assert.Equal("@a\n", _field.LfText);
    }

    [Fact]
    public void The_list_closes_when_the_box_loses_the_keyboard_focus()
    {
        UseContacts();
        _field.Type("@a");

        _field.LoseFocus();

        Assert.False(_list.IsOpen);
        Assert.Equal("@a", _field.Text);
    }

    [Fact]
    public void The_list_closes_when_another_list_opens()
    {
        UseContacts();
        _field.Type("@a");

        _popups.Show(_other, ["Red"]);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Esc_closes_the_list_and_keeps_the_typed_text()
    {
        UseContacts();
        _field.Type("@bo");

        Assert.True(_popups.CloseOpen());

        Assert.False(_list.IsOpen);
        Assert.Equal("@bo", _field.Text);
    }

    [Fact]
    public void Enter_with_nothing_moved_to_closes_the_list_and_inserts_nothing()
    {
        UseContacts();
        _field.Type("@bo");

        Assert.False(_popups.ChooseMovedOrClose());

        Assert.False(_list.IsOpen);
        Assert.Equal("@bo", _field.Text);
    }

    [Fact]
    public void Tab_closes_the_list_and_is_left_for_the_box_to_move_the_focus()
    {
        UseContacts();
        _field.Type("@bo");

        Assert.False(_picking.Tab());

        Assert.False(_list.IsOpen);
        Assert.Equal("@bo", _field.Text);
    }

    [Fact]
    public void Tab_with_the_list_closed_is_left_for_the_box_too()
    {
        UseContacts();

        Assert.False(_picking.Tab());
    }

    [Fact]
    public void Choosing_replaces_the_at_sign_and_the_typed_text_with_an_at_sign_and_the_alias_with_no_space_added()
    {
        UseContacts();
        _field.Type("see @ana");

        _list.Choose(0);

        Assert.Equal("see @anap", _field.Text);
        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Choosing_does_not_move_a_mention_template()
    {
        UseContacts();
        var file = TestContactStore.PathOf(_services.Clone, TestContactStore.TemplatesFile);
        var before = _services.Files.ReadFile(file);
        _field.Type("@");

        _list.Choose(1);

        Assert.Equal(before, _services.Files.ReadFile(file));
        Assert.Equal(["{fullName} ({title})", "{firstName}"], _services.Contacts.MentionTemplates);
    }

    [Fact]
    public void Choosing_a_contact_with_no_alias_inserts_its_ID()
    {
        UseContacts();
        _field.Type("@bo");

        _list.Choose(0);

        Assert.Equal("@bob", _field.Text);
    }

    [Fact]
    public void Down_then_Enter_chooses_the_contact_moved_to()
    {
        UseContacts();
        _field.Type("@");
        _popups.MoveDown();
        _popups.MoveDown();
        _popups.MoveDown();

        Assert.True(_popups.ChooseMovedOrClose());

        Assert.Equal("@ci", _field.Text);
    }

    [Fact]
    public void Down_and_Up_stop_at_the_first_and_the_last_row()
    {
        UseContacts();
        _field.Type("@");

        _popups.MoveDown();
        _popups.MoveUp();
        _popups.MoveUp();
        Assert.Equal(0, _list.SelectedIndex);
        for (var i = 0; i < 12; i++)
        {
            _popups.MoveDown();
        }

        Assert.Equal(6, _list.SelectedIndex);
    }

    [Fact]
    public void Choosing_puts_the_cursor_right_after_the_alias_and_gives_the_box_the_keyboard_focus()
    {
        UseContacts();
        _field.Type("a @ana");

        _list.Choose(0);

        Assert.Equal(_field.Text.Length, _field.CaretOffset);
        Assert.Equal(1, _field.FocusCount);
    }

    [Fact]
    public void Text_after_the_cursor_stays_after_the_alias()
    {
        UseContacts();
        var field = new FakeTextField(" tail", 0);
        var list = new ChoiceList();
        _ = new ContactAliasPicking(field, list, new PopupLists(list), _services.Contacts);
        field.Type("@ana");

        list.Choose(0);

        Assert.Equal("@anap tail", field.Text);
    }

    [Fact]
    public void One_undo_takes_the_alias_back_to_the_typed_text()
    {
        UseContacts();
        _field.Type("see @ana");
        _list.Choose(0);

        _field.Undo();

        Assert.Equal("see @ana", _field.Text);
    }

    [Fact]
    public void The_inserted_alias_opens_no_list_again()
    {
        UseContacts();
        _field.Type("@ana");
        _list.Choose(0);

        _field.Type("p");

        Assert.False(_list.IsOpen);
    }
}
