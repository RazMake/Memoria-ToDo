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

    private IEnumerable<string> FullNames => _picking.Rows.Select(row => row.FullName);

    private void UseNineContacts(params string[] templates) =>
        _services.UseContacts(templates.Length == 0 ? [WithTitle, FirstName, FullName] : templates,
            ContactFiles.Person("ana", "Ana Pop", ("firstName", "Ana"), ("title", "Engineer"), ("alias", "anap")),
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
        Assert.Equal(["Ana /Pop"], FullNames);
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

        Assert.Equal(["Ana Pop", "Bob Stan", "Cris Ilie", "Dan Rus", "Eva Mic", "Fay Nan", "Gus Oro"], FullNames);
    }

    [Fact]
    public void Typing_after_the_at_sign_narrows_the_list_by_Contacts_search()
    {
        UseNineContacts(FullName);

        _field.Type("@bo");

        Assert.Equal(["Bob Stan"], FullNames);
    }

    [Fact]
    public void The_search_ignores_case_and_may_hold_spaces()
    {
        UseNineContacts(FullName);

        _field.Type("@ANA p");

        Assert.Equal(["Ana Pop"], FullNames);
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

        Assert.Equal(["Bob Stan"], FullNames);
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
    public void Each_row_has_the_contacts_full_name_and_its_alias()
    {
        UseNineContacts(WithTitle, FirstName);

        _field.Type("@a");

        Assert.Equal(new MentionRow("Ana Pop", "anap"), _picking.Rows[0]);
    }

    [Fact]
    public void A_row_of_a_contact_with_no_alias_has_its_ID_in_the_aliass_place()
    {
        UseNineContacts(FullName);

        _field.Type("@bob");

        Assert.Equal([new MentionRow("Bob Stan", "bob")], _picking.Rows);
    }

    [Fact]
    public void The_lists_items_are_each_rows_plain_text_as_Full_Name_then_the_alias_in_parentheses()
    {
        UseNineContacts(FullName);

        _field.Type("@");

        Assert.Equal(["Ana Pop (anap)", "Bob Stan (bob)"], _list.Items.Take(2));
        Assert.Equal(_picking.Rows.Select(row => $"{row.FullName} ({row.Alias})"), _list.Items);
    }

    [Fact]
    public void The_rows_do_not_show_the_template_and_stay_the_same_when_the_template_changes()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@ana");
        var before = _picking.Rows.ToList();

        _picking.NextTemplate();

        Assert.Equal(before, _picking.Rows);
        Assert.Equal(["Ana Pop (anap)"], _list.Items);
    }

    [Fact]
    public void The_rows_are_empty_while_the_list_is_closed()
    {
        UseNineContacts(FullName);

        Assert.Empty(_picking.Rows);
        Assert.Equal("", _picking.Header);
        Assert.Equal("", _picking.TemplateHint);
    }

    [Fact]
    public void The_header_shows_the_first_contact_in_the_current_template_while_none_is_moved_to()
    {
        UseNineContacts(WithTitle, FirstName);

        _field.Type("@");

        Assert.Equal("Ana Pop (Engineer)", _picking.Header);
    }

    [Fact]
    public void The_header_shows_a_contact_that_lacks_a_value_the_template_names_by_its_full_name()
    {
        UseNineContacts(WithTitle, FirstName);

        _field.Type("@bob");

        Assert.Equal("Bob Stan", _picking.Header);
    }

    [Fact]
    public void The_header_follows_Down_and_Up_to_the_contact_moved_to()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@");

        _popups.MoveDown();
        _popups.MoveDown();
        _popups.MoveDown();
        Assert.Equal("Cris Ilie (Designer)", _picking.Header);

        _popups.MoveUp();
        Assert.Equal("Bob Stan", _picking.Header);
    }

    [Fact]
    public void The_header_shows_the_filtered_first_contact_after_typing_narrows_the_list()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@");

        _field.Type("cr");

        Assert.Equal("Cris Ilie (Designer)", _picking.Header);
    }

    [Fact]
    public void The_template_hint_shows_while_there_are_two_or_more_templates()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@");

        Assert.Equal("Tab or click: next template", _picking.TemplateHint);
    }

    [Fact]
    public void The_template_hint_is_empty_with_one_template()
    {
        UseNineContacts(FullName);
        _field.Type("@");

        Assert.Equal("", _picking.TemplateHint);
    }

    [Fact]
    public void Down_and_Up_stop_at_the_first_and_the_last_row()
    {
        UseNineContacts(FullName);
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
        _popups.ChooseMovedOrClose();
        Assert.Equal("[Gus Oro](contact:gus \"{fullName}\")", _field.Text);
    }

    [Fact]
    public void Up_after_Down_chooses_the_contact_before()
    {
        UseNineContacts(FullName);
        _field.Type("@");
        _popups.MoveDown();
        _popups.MoveDown();
        _popups.MoveUp();

        _popups.ChooseMovedOrClose();

        Assert.Equal("[Ana Pop](contact:ana \"{fullName}\")", _field.Text);
    }

    [Fact]
    public void Tab_moves_to_the_next_template_and_the_header_updates_at_once_while_the_rows_stay()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@ana");

        Assert.True(_picking.NextTemplate());

        Assert.Equal("Ana", _picking.Header);
    }

    [Fact]
    public void Tab_goes_back_to_the_first_template_after_the_last()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@ana");

        _picking.NextTemplate();
        _picking.NextTemplate();

        Assert.Equal("Ana Pop (Engineer)", _picking.Header);
    }

    [Fact]
    public void Tab_follows_the_files_order_through_three_templates()
    {
        UseNineContacts(WithTitle, FirstName, FullName);
        _field.Type("@ana");

        _picking.NextTemplate();
        _picking.NextTemplate();

        Assert.Equal("Ana Pop", _picking.Header);
    }

    [Fact]
    public void Tab_keeps_the_row_moved_to_and_the_header_shows_that_contact_in_the_next_template()
    {
        UseNineContacts(WithTitle, "{title}");
        _field.Type("@");
        _popups.MoveDown();
        _popups.MoveDown();
        _popups.MoveDown();

        _picking.NextTemplate();

        Assert.Equal(2, _list.SelectedIndex);
        Assert.Equal("Designer", _picking.Header);
        _popups.ChooseMovedOrClose();
        Assert.Equal("[Designer](contact:cris \"{title}\")", _field.Text);
    }

    [Fact]
    public void Each_opening_starts_at_the_first_template()
    {
        UseNineContacts(WithTitle, FirstName);
        _field.Type("@ana");
        _picking.NextTemplate();
        _popups.CloseOpen();

        _field.Type(" @ana");

        Assert.Equal("Ana Pop (Engineer)", _picking.Header);
    }

    [Fact]
    public void With_one_template_Tab_does_nothing_and_still_stays_in_the_field()
    {
        UseNineContacts(FullName);
        _field.Type("@ana");

        Assert.True(_picking.NextTemplate());

        Assert.Equal("Ana Pop", _picking.Header);
        Assert.True(_list.IsOpen);
    }

    [Fact]
    public void Tab_with_the_list_closed_is_left_for_the_field_to_handle()
    {
        UseNineContacts(WithTitle, FirstName);

        Assert.False(_picking.NextTemplate());
    }

    [Fact]
    public void The_list_closes_when_the_field_loses_the_keyboard_focus_and_the_typed_text_stays()
    {
        UseNineContacts(FullName);
        _field.Type("@bo");

        _field.LoseFocus();

        Assert.False(_list.IsOpen);
        Assert.Equal("@bo", _field.Text);
    }

    [Fact]
    public void Losing_the_focus_with_no_list_open_does_nothing()
    {
        UseNineContacts(FullName);

        _field.LoseFocus();

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void After_the_focus_is_lost_a_new_at_sign_opens_the_list_again()
    {
        UseNineContacts(FullName);
        _field.Type("@bo");
        _field.LoseFocus();

        _field.Type(" @bo");

        Assert.Equal(["Bob Stan"], FullNames);
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

    [Fact]
    public void The_header_is_the_templates_Markdown_with_each_value_escaped()
    {
        _services.UseContacts(["{fullName} (_{alias}_)"], ContactFiles.Person("ana", "Ana *QA*", ("alias", "ana_pop")));

        _field.Type("@");

        Assert.Equal("Ana \\*QA\\* (_ana\\_pop_)", _picking.Header);
    }

    // Erasing back into a partial mention opens the list as typing it would have. "al" finds Alan and Alex; "ale" only Alex.
    private const string Alan = "[Alan Rus](contact:alan \"{fullName}\")";

    private void UseAlans() =>
        _services.UseContacts([FullName],
            ContactFiles.Person("alan", "Alan Rus"),
            ContactFiles.Person("alex", "Alex Mic"),
            ContactFiles.Person("bob", "Bob Stan"));

    // "@ale" typed, then the list closed by Esc, so only an erase can open it again.
    private void TypeAleAndPressEsc()
    {
        UseAlans();
        _field.Type("@ale");
        Assert.True(_popups.CloseOpen());
        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Backspace_erasing_back_into_a_partial_mention_opens_the_list_for_it_after_Esc()
    {
        TypeAleAndPressEsc();

        _field.Backspace();

        Assert.Equal(["Alan Rus", "Alex Mic"], FullNames);
        Assert.Equal("@al", _field.Text);
    }

    [Fact]
    public void Backspace_erasing_back_into_a_partial_mention_opens_the_list_after_the_field_lost_the_focus()
    {
        UseAlans();
        _field.Type("@ale");
        _field.LoseFocus();
        Assert.False(_list.IsOpen);

        _field.Backspace();

        Assert.Equal(["Alan Rus", "Alex Mic"], FullNames);
    }

    [Fact]
    public void Backspace_erasing_back_into_a_partial_mention_opens_the_list_in_a_field_that_opened_with_the_text()
    {
        UseAlans();
        _field.Document.Text = "see @ale";
        _field.Cursor(8);
        Assert.False(_list.IsOpen);

        _field.Backspace();

        Assert.Equal(["Alan Rus", "Alex Mic"], FullNames);
    }

    [Fact]
    public void Delete_erasing_back_into_a_partial_mention_opens_the_list()
    {
        TypeAleAndPressEsc();
        _field.Cursor(3);

        _field.Delete();

        Assert.Equal("@al", _field.Text);
        Assert.Equal(["Alan Rus", "Alex Mic"], FullNames);
    }

    [Fact]
    public void Erasing_a_word_with_Ctrl_Backspace_back_into_a_partial_mention_opens_the_list()
    {
        UseAlans();
        _field.Type("@alan");
        _popups.CloseOpen();

        _field.Erase(3, 2);

        Assert.Equal("@al", _field.Text);
        Assert.Equal(["Alan Rus", "Alex Mic"], FullNames);
    }

    [Fact]
    public void Cutting_a_selection_back_into_a_partial_mention_opens_the_list()
    {
        UseAlans();
        _field.Type("@alan");
        _popups.CloseOpen();
        _field.Select(3, 2);

        _field.Cut();

        Assert.Equal("@al", _field.Text);
        Assert.Equal(["Alan Rus", "Alex Mic"], FullNames);
    }

    [Fact]
    public void An_undo_that_only_removes_text_back_into_a_partial_mention_opens_the_list()
    {
        TypeAleAndPressEsc();

        _field.Undo();

        Assert.Equal("@al", _field.Text);
        Assert.Equal(["Alan Rus", "Alex Mic"], FullNames);
    }

    [Fact]
    public void Erasing_back_to_a_bare_at_sign_opens_the_list_with_the_first_seven_contacts()
    {
        UseNineContacts(FullName);
        _field.Type("@ana");
        _popups.CloseOpen();

        _field.Erase(1, 3);

        Assert.Equal("@", _field.Text);
        Assert.Equal(["Ana Pop", "Bob Stan", "Cris Ilie", "Dan Rus", "Eva Mic", "Fay Nan", "Gus Oro"], FullNames);
    }

    [Fact]
    public void Erasing_the_at_sign_itself_opens_nothing()
    {
        UseNineContacts(FullName);
        _field.Type("@");
        _popups.CloseOpen();

        _field.Backspace();

        Assert.Equal("", _field.Text);
        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Erasing_text_before_the_at_sign_opens_nothing()
    {
        UseAlans();
        _field.Type("x @ale");
        _popups.CloseOpen();
        _field.Cursor(1);

        _field.Backspace();

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Choosing_after_erasing_back_into_a_partial_mention_replaces_the_at_sign_and_the_rest()
    {
        TypeAleAndPressEsc();
        _field.Backspace();

        _list.Choose(1);

        Assert.Equal("[Alex Mic](contact:alex \"{fullName}\")", _field.Text);
    }

    [Fact]
    public void Typing_over_a_selection_is_typing_so_it_opens_nothing()
    {
        TypeAleAndPressEsc();
        _field.Select(1, 3);

        _field.Type("a");

        Assert.Equal("@a", _field.Text);
        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Moving_the_cursor_back_into_a_partial_mention_opens_nothing()
    {
        TypeAleAndPressEsc();

        _field.Cursor(3);
        _field.Cursor(2);
        _field.Cursor(4);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Erasing_opens_nothing_while_another_list_is_open()
    {
        TypeAleAndPressEsc();
        _popups.Show(_other, ["Red"]);

        _field.Backspace();

        Assert.False(_list.IsOpen);
        Assert.True(_other.IsOpen);
    }

    [Fact]
    public void Erasing_opens_nothing_when_a_chip_lies_between_the_at_sign_and_the_cursor()
    {
        // Search needs every word of the text between the "@" and the cursor, the chip's Markdown included, in one field: only this title has them.
        _services.UseContacts([FullName], ContactFiles.Person("ref", "Ref Mic", ("title", $"{Alan}ale")));
        _field.Document.Text = $"@{Alan}ale";
        _field.Cursor(_field.Text.Length);

        _field.Backspace();

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Erasing_opens_nothing_for_an_at_sign_inside_a_mention_shown_as_Markdown_across_a_line_break()
    {
        UseAlans();
        _field.Document.Text = "[Ana\n@ale](contact:ana \"{fullName}\")";
        _field.Cursor("[Ana\n@ale".Length);

        _field.Backspace();

        Assert.Equal("[Ana\n@al](contact:ana \"{fullName}\")", _field.LfText);
        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Erasing_while_the_list_is_open_keeps_narrowing_it()
    {
        UseAlans();
        _field.Type("@alex");
        Assert.Equal(["Alex Mic"], FullNames);

        _field.Backspace();
        _field.Backspace();

        Assert.Equal(["Alan Rus", "Alex Mic"], FullNames);
    }
}
