using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>The Contacts window's common scenarios on its one screen, with real Contacts over an in-memory clone. Rules and edge cases are in the unit tests.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ContactsWindowTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private readonly ClosePrompt _prompt = new("Contacts");

    private static ContactsScreen ChangedForm(ContactServices services)
    {
        var screen = new ContactsScreen(services);
        screen.ClickRow("Bob Stan");
        screen.ClickButton("Edit");
        screen.Type("title", "Lead");
        return screen;
    }

    [WpfFact]
    public void The_close_button_and_Alt_F4_ask_the_same_prompt_and_Cancel_keeps_Contacts_open_with_the_form()
    {
        using var screen = ChangedForm(_services);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            ClosePrompt.PressCloseButton(window);
            return 0;
        }, ClosePrompt.Click("Cancel"));

        Assert.Equal(1, _prompt.Shown);
        Assert.True(screen.View.IsOpen);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.Equal("Designer", _services.Member("team", "bob").Title);
    }

    [WpfFact]
    public void The_Close_window_keys_ask_the_same_prompt_and_Cancel_keeps_Contacts_open_with_the_form()
    {
        _services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        using var screen = ChangedForm(_services);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            SimulatedKey.Chord(window, Key.LeftShift, Key.F9);
            return 0;
        }, ClosePrompt.Click("Cancel"));

        Assert.Equal(1, _prompt.Shown);
        Assert.True(screen.View.IsOpen);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.Equal("Designer", _services.Member("team", "bob").Title);
    }

    [WpfFact]
    public void A_plain_WM_CLOSE_with_a_changed_form_closes_without_asking_and_discards_the_form()
    {
        using var screen = ChangedForm(_services);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            ClosePrompt.SendPlainClose(window);
            return 0;
        }, ClosePrompt.Click("Cancel"));

        Assert.Equal(0, _prompt.Shown);
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Designer", _services.Member("team", "bob").Title);
    }

    [WpfFact]
    public void A_plain_WM_CLOSE_while_the_prompt_shows_closes_the_prompt_as_Cancel_then_closes_Contacts_and_discards_the_form()
    {
        using var screen = ChangedForm(_services);
        var window = screen.Window;

        _prompt.Run(() =>
        {
            ClosePrompt.PressCloseButton(window);
            return 0;
        }, _ => ClosePrompt.SendPlainClose(window));

        Assert.Equal(1, _prompt.Shown);
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Designer", _services.Member("team", "bob").Title);
    }

    [WpfFact]
    public void Opening_shows_the_groups_with_their_contacts_and_asks_to_pick_one()
    {
        using var screen = new ContactsScreen(_services);

        Assert.Equal(["Me", "Team", "Ex-Team", "Friends", "Empty"], screen.GroupNames);
        Assert.Equal(["Ana Pop", "Bob Stan"], screen.RowNamesIn("Team"));
        Assert.Contains("Pick a contact to see the details.", screen.DetailsTexts);
    }

    [WpfFact]
    public void Clicking_a_contact_shows_its_details_on_the_right()
    {
        using var screen = new ContactsScreen(_services);

        screen.ClickRow("Bob Stan");

        Assert.Equal(["Bob Stan"], screen.SelectedNames);
        Assert.Contains("Designer", screen.DetailsTexts);
    }

    [WpfFact]
    public void Typing_in_the_search_leaves_the_contacts_that_match()
    {
        using var screen = new ContactsScreen(_services);

        screen.Search("oradea");

        Assert.Equal(["Friends"], screen.GroupNames);
        Assert.Equal(["Dan Rus"], screen.RowNames);
    }

    [WpfFact]
    public void Editing_a_contact_and_pressing_the_Save_keys_saves_it_and_shows_it_selected()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Bob Stan");

        screen.ClickButton("Edit");
        screen.Type("title", "Lead");
        screen.PressSaveKeys();

        Assert.Equal("Lead", _services.Member("team", "bob").Title);
        Assert.Equal(["Bob Stan"], screen.SelectedNames);
        Assert.Contains("Lead", screen.DetailsTexts);
    }

    [WpfFact]
    public void Adding_a_contact_to_a_group_saves_it_and_lists_it()
    {
        using var screen = new ContactsScreen(_services);

        screen.ClickButton("Add", inGroup: "Friends");
        screen.Type("id", "eva");
        screen.Type("fullName", "Eva Marin");
        screen.PressSaveKeys();

        Assert.Equal(["Dan Rus", "Eva Marin"], screen.RowNamesIn("Friends"));
        Assert.Equal(["Eva Marin"], screen.SelectedNames);
    }

    [WpfFact]
    public void Deleting_a_contact_after_confirming_removes_it_from_the_list()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Cris Ilie");

        screen.DeleteAnsweringWith("Delete");

        Assert.DoesNotContain("Cris Ilie", screen.RowNames);
        Assert.Empty(_services.Group("ex-team").Members);
    }

    [WpfFact]
    public void Dragging_a_groups_heading_onto_another_moves_the_group_before_it()
    {
        using var screen = new ContactsScreen(_services);

        var taken = screen.DragGroup("Friends", "Team");

        Assert.True(taken);
        Assert.Equal(["Me", "Friends", "Team", "Ex-Team", "Empty"], screen.GroupNames);
        Assert.Equal(["me", "friends", "team", "ex-team", "empty"], _services.Contacts.Groups.Select(group => group.Name));
    }
}
