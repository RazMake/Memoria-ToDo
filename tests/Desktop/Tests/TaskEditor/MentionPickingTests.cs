using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Typing "@" opens the contact list, which narrows as the user types, shows each contact in the current template, moves to the
/// next template on Tab, and inserts the chosen contact's mention. Where an "@" opens the list, what text it searches with, and
/// where the mention goes are Editing's rules; which contacts match, and the mention's Markdown, are Contacts'. Tested here
/// over the real Contacts, with the files seeded as literal JSON.
/// </summary>
[Unit]
public class MentionPickingTests
{
    private const string WithTitle = "{fullName} ({title})";
    private const string FirstName = "{firstName}";
    private const string FullName = "{fullName}";

    private readonly EditorServices _services = new();
    private readonly ChoiceList _list = new();
    private readonly ChoiceList _other = new();
    private readonly PopupLists _popups;
    private readonly FakeTextField _field = new();
    private readonly MentionPicking _picking;

    public MentionPickingTests()
    {
        _popups = new PopupLists(_other, _list);
        _picking = new MentionPicking(_field, _list, _popups, _services.Contacts);
    }

    private void UseNineContacts(params string[] templates) =>
        _services.UseContacts(templates.Length == 0 ? [WithTitle, FirstName, FullName] : templates,
            ContactFiles.Person("ana", "Ana Pop", ("firstName", "Ana"), ("title", "Engineer")),
            ContactFiles.Person("bob", "Bob Stan"),
            ContactFiles.Person("cris", "Cris Ilie", ("title", "Designer")),
            ContactFiles.Person("dan", "Dan Rus"),
            ContactFiles.Person("eva", "Eva Mic"),
            ContactFiles.Person("fay", "Fay Nan"),
            ContactFiles.Person("gus", "Gus Oro"),
            ContactFiles.Person("hal", "Hal Pax"),
            ContactFiles.Person("ira", "Ira Qui"));

    [Fact]
    public void Typing_an_at_sign_lists_the_contacts()
    {
        UseNineContacts(FullName);

        _field.Type("@");

        Assert.True(_list.IsOpen);
        Assert.Equal("@", _field.Text);
    }

    [Fact]
    public void Typing_an_at_sign_in_the_open_snippet_list_does_not_open_the_contact_list()
    {
        UseNineContacts(FullName);
        WireSnippets();

        _field.Type("/");
        Assert.True(_other.IsOpen);
        _field.Type("@");

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Typing_a_slash_in_the_open_contact_list_does_not_open_the_snippet_list()
    {
        _services.UseContacts([FullName], ContactFiles.Person("ap", "Ana /Pop"), ContactFiles.Person("bob", "Bob Stan"));
        WireSnippets();

        _field.Type("@Ana ");
        Assert.True(_list.IsOpen);
        _field.Type("/");

        Assert.False(_other.IsOpen);
        Assert.Equal(["Ana /Pop"], _list.Items);
    }

    // The other list of the popups stands for the snippet list, as the editor shares its popups between the pickers of a field.
    private void WireSnippets() =>
        _ = new SnippetPicking(_field, _other, _popups,
            new SnippetInserter(new FakeTimeProvider(), new FakeClipboard(), (_, _, _) => { }, (_, _, _) => { }));

    [Fact]
    public void An_at_sign_after_a_space_opens_the_list_too()
    {
        UseNineContacts(FullName);

        _field.Type("hi @");

        Assert.True(_list.IsOpen);
    }

    [Fact]
    public void An_at_sign_after_a_letter_opens_no_list_and_Editing_decides_that()
    {
        UseNineContacts(FullName);

        _field.Type("a@");

        Assert.False(_list.IsOpen);
        Assert.Equal("a@", _field.Text);
    }

    [Fact]
    public void Text_typed_without_an_at_sign_opens_no_list()
    {
        UseNineContacts(FullName);

        _field.Type("bob");

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Opening_closes_any_other_open_list()
    {
        UseNineContacts(FullName);
        _popups.Show(_other, ["Red"]);

        _field.Type("@");

        Assert.True(_list.IsOpen);
        Assert.False(_other.IsOpen);
    }

    [Fact]
    public void With_nothing_typed_it_lists_the_first_seven_contacts_by_full_name()
    {
        UseNineContacts(FullName);

        _field.Type("@");

        Assert.Equal(["Ana Pop", "Bob Stan", "Cris Ilie", "Dan Rus", "Eva Mic", "Fay Nan", "Gus Oro"], _list.Items);
    }

    [Fact]
    public void Typing_after_the_at_sign_narrows_the_list_by_Contacts_search()
    {
        UseNineContacts(FullName);

        _field.Type("@bo");

        Assert.Equal(["Bob Stan"], _list.Items);
    }

    [Fact]
    public void The_search_ignores_case_and_may_hold_spaces()
    {
        UseNineContacts(FullName);

        _field.Type("@ANA p");

        Assert.Equal(["Ana Pop"], _list.Items);
    }

    [Fact]
    public void The_list_closes_when_no_contact_matches_and_the_typed_text_stays()
    {
        UseNineContacts(FullName);
        _field.Type("@");
        Assert.True(_list.IsOpen);

        _field.Type("zzz");

        Assert.False(_list.IsOpen);
        Assert.Equal("@zzz", _field.Text);
    }

    [Fact]
    public void Erasing_back_to_a_match_opens_the_list_again()
    {
        UseNineContacts(FullName);
        _field.Type("@bx");
        Assert.False(_list.IsOpen);

        _field.Backspace();

        Assert.Equal(["Bob Stan"], _list.Items);
    }

    [Fact]
    public void The_list_closes_when_the_at_sign_is_erased()
    {
        UseNineContacts(FullName);
        _field.Type("@a");
        Assert.True(_list.IsOpen);

        _field.Backspace();
        _field.Backspace();

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void The_list_closes_when_the_cursor_moves_before_the_at_sign()
    {
        UseNineContacts(FullName);
        _field.Type("ab @a");
        Assert.True(_list.IsOpen);

        _field.Cursor(1);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void The_list_closes_when_the_cursor_moves_to_just_before_the_at_sign()
    {
        UseNineContacts(FullName);
        _field.Type("ab @a");
        Assert.True(_list.IsOpen);

        _field.Cursor(3);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void The_list_closes_when_a_line_break_is_typed_after_the_at_sign_and_the_text_stays()
    {
        UseNineContacts(FullName);
        _field.Type("@a");
        Assert.True(_list.IsOpen);

        _field.Type("\n");

        Assert.False(_list.IsOpen);
        Assert.Equal("@a\n", _field.LfText);
    }

    [Fact]
    public void With_no_repository_no_list_opens_and_the_at_sign_stays()
    {
        _field.Type("@");

        Assert.False(_list.IsOpen);
        Assert.Equal("@", _field.Text);
    }

    [Fact]
    public void With_a_repository_but_no_contacts_no_list_opens_and_the_at_sign_stays()
    {
        _services.UseContacts([FullName]);

        _field.Type("@");

        Assert.False(_list.IsOpen);
        Assert.Equal("@", _field.Text);
    }

    [Fact]
    public void Down_then_Enter_chooses_the_first_contact()
    {
        UseNineContacts(FullName);
        _field.Type("see @");
        _popups.MoveDown();

        Assert.True(_popups.ChooseMovedOrClose());

        Assert.Equal("see [Ana Pop](contact:ana \"{fullName}\")", _field.Text);
    }

    [Fact]
    public void Down_twice_then_Enter_chooses_the_second_contact()
    {
        UseNineContacts(FullName);
        _field.Type("@");
        _popups.MoveDown();
        _popups.MoveDown();

        _popups.ChooseMovedOrClose();

        Assert.Equal("[Bob Stan](contact:bob \"{fullName}\")", _field.Text);
    }

    [Fact]
    public void Enter_with_nothing_moved_to_closes_the_list_and_inserts_nothing()
    {
        UseNineContacts(FullName);
        _field.Type("@bo");

        Assert.False(_popups.ChooseMovedOrClose());

        Assert.False(_list.IsOpen);
        Assert.Equal("@bo", _field.Text);
    }

    [Fact]
    public void Esc_closes_the_list_and_keeps_the_typed_text()
    {
        UseNineContacts(FullName);
        _field.Type("@bo");

        Assert.True(_popups.CloseOpen());

        Assert.False(_list.IsOpen);
        Assert.Equal("@bo", _field.Text);
    }

    [Fact]
    public void A_click_chooses_the_contact_clicked()
    {
        UseNineContacts(FullName);
        _field.Type("@");

        _list.Choose(2);

        Assert.Equal("[Cris Ilie](contact:cris \"{fullName}\")", _field.Text);
    }

    [Fact]
    public void Each_row_shows_the_contact_text_in_the_current_template()
    {
        UseNineContacts(WithTitle, FirstName);

        _field.Type("@a");

        Assert.Equal("Ana Pop (Engineer)", _list.Items[0]);
    }

    [Fact]
    public void A_contact_that_lacks_a_value_the_template_names_shows_its_full_name()
    {
        UseNineContacts(WithTitle, FirstName);

        _field.Type("@bob");

        Assert.Equal(["Bob Stan"], _list.Items);
    }

    [Fact]
    public void Each_row_has_the_contacts_title_for_the_help_style_and_none_when_the_contact_has_no_title()
    {
        UseNineContacts(FullName);

        _field.Type("@");

        Assert.Equal(["Engineer", null, "Designer", null, null, null, null], _picking.Titles);
    }

    [Fact]
    public void The_last_line_shows_the_current_template_and_the_Tab_hint()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@");

        Assert.Contains(WithTitle, _picking.Hint);
        Assert.Contains("Tab: next template", _picking.Hint);
    }

    [Fact]
    public void Tab_moves_to_the_next_template_and_the_rows_and_the_last_line_update_at_once()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@ana");

        Assert.True(_picking.NextTemplate());

        Assert.Equal(["Ana"], _list.Items);
        Assert.Contains(FirstName, _picking.Hint);
        Assert.DoesNotContain(WithTitle, _picking.Hint);
    }

    [Fact]
    public void Tab_goes_back_to_the_first_template_after_the_last()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@ana");

        _picking.NextTemplate();
        _picking.NextTemplate();

        Assert.Equal(["Ana Pop (Engineer)"], _list.Items);
        Assert.Contains(WithTitle, _picking.Hint);
    }

    [Fact]
    public void Tab_follows_the_files_order_through_three_templates()
    {
        UseNineContacts(WithTitle, FirstName, FullName);
        _field.Type("@ana");

        _picking.NextTemplate();
        _picking.NextTemplate();

        Assert.Equal(["Ana Pop"], _list.Items);
    }

    [Fact]
    public void Each_opening_starts_at_the_first_template()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@ana");
        _picking.NextTemplate();
        _popups.CloseOpen();

        _field.Type(" @ana");

        Assert.Equal(["Ana Pop (Engineer)"], _list.Items);
    }

    [Fact]
    public void With_one_template_Tab_does_nothing_and_still_stays_in_the_field()
    {
        UseNineContacts(FullName);
        _field.Type("@ana");

        Assert.True(_picking.NextTemplate());

        Assert.Equal(["Ana Pop"], _list.Items);
        Assert.Contains(FullName, _picking.Hint);
    }

    [Fact]
    public void Tab_with_the_list_closed_is_left_for_the_field_to_handle()
    {
        UseNineContacts(WithTitle, FirstName);

        Assert.False(_picking.NextTemplate());
    }

    [Fact]
    public void Choosing_inserts_the_mention_in_place_of_the_at_sign_and_what_was_typed_and_closes_the_list()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("see @ana");

        _list.Choose(0);

        Assert.Equal("see [Ana Pop (Engineer)](contact:ana \"{fullName} ({title})\")", _field.Text);
        Assert.False(_list.IsOpen);
        Assert.Equal(1, _field.FocusCount);
    }

    [Fact]
    public void Choosing_after_Tab_inserts_the_mention_of_the_next_template()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@ana");
        _picking.NextTemplate();

        _list.Choose(0);

        Assert.Equal("[Ana](contact:ana \"{firstName}\")", _field.Text);
    }

    [Fact]
    public void Choosing_puts_the_cursor_right_after_the_mention()
    {
        UseNineContacts(FullName);
        _field.Type("a @bo");

        _list.Choose(0);

        Assert.Equal(_field.Text.Length, _field.CaretOffset);
    }

    [Fact]
    public void One_undo_takes_the_mention_back_to_the_typed_text()
    {
        UseNineContacts(FullName);
        _field.Type("see @bo");
        _list.Choose(0);

        _field.Undo();

        Assert.Equal("see @bo", _field.Text);
    }
}
