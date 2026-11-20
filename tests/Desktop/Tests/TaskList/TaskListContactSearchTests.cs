using System.Windows.Input;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskEditor.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Contacts in Task View's search and on its mentions, on the common paths: "@" in the search box offers the contacts and chooses an alias,
/// the list narrows to the tasks that mention the contact, Esc closes the "@" list before it empties the search, and resting on a mention in a row's
/// title, in the selected task's description or its edit's Preview, or in an open edit's chip has the contact's card. Which words name a contact is in
/// <see cref="ListFilterContactTests"/>; when the card opens is the platform's, and which mention the web view finds is the adapter's.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListContactSearchTests
{
    private const string Ana = "[Old Name](contact:ana \"{fullName}\")";
    private const string Zed = "[Zed Old](contact:zed \"{fullName}\")";

    private static ListServices Services()
    {
        var services = new ListServices();
        services.UseContacts(
            ["{fullName}"],
            ContactFiles.Person("ana", "Ana Pop", ("alias", "anap"), ("title", "Engineer")),
            ContactFiles.Person("bob", "Bob Stan"));
        services.Add($"Call {Ana} today");
        services.Add($"Call [Bob Stan](contact:bob \"{{fullName}}\") today");
        services.Add("Buy milk");
        return services;
    }

    [WpfFact]
    public void Typing_an_at_sign_in_the_search_box_offers_the_contacts_and_keeps_the_at_sign()
    {
        using var screen = new TaskListScreen(Services());

        screen.TypeInSearch("@");

        Assert.Equal(["Ana Pop (anap)", "Bob Stan (bob)"], screen.ShownAliases);
        Assert.Equal("@", screen.SearchText);
    }

    [WpfFact]
    public void Choosing_a_contact_puts_the_at_sign_and_its_alias_in_the_box_and_narrows_the_list_to_its_mentions()
    {
        using var screen = new TaskListScreen(Services());
        screen.TypeInSearch("@an");

        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal("@anap", screen.SearchText);
        Assert.Empty(screen.ShownAliases);
        Assert.Equal(["Call Ana Pop today"], screen.RowTitles);
    }

    [WpfFact]
    public void Choosing_a_contact_with_no_alias_puts_the_at_sign_and_its_id_in_the_box()
    {
        using var screen = new TaskListScreen(Services());
        screen.TypeInSearch("@bo");

        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal("@bob", screen.SearchText);
        Assert.Equal(["Call Bob Stan today"], screen.RowTitles);
    }

    [WpfFact]
    public void Typing_an_alias_by_hand_narrows_the_list_to_the_tasks_that_mention_the_contact()
    {
        using var screen = new TaskListScreen(Services());

        screen.TypeSearch("@anap");

        Assert.Equal(["Call Ana Pop today"], screen.RowTitles);
    }

    [WpfFact]
    public void An_at_word_that_names_nobody_narrows_the_list_as_a_plain_word()
    {
        var services = Services();
        services.Add("Mail @nobody");
        using var screen = new TaskListScreen(services);

        screen.TypeSearch("@nobody");

        Assert.Equal(["Mail @nobody"], screen.RowTitles);
    }

    [WpfFact]
    public void The_list_shows_the_tasks_again_when_the_at_word_is_cleared()
    {
        using var screen = new TaskListScreen(Services());
        screen.TypeSearch("@anap");

        screen.TypeSearch("");

        Assert.Equal(3, screen.RowTitles.Count);
    }

    [WpfFact]
    public void Down_with_the_at_list_open_moves_in_the_list_and_selects_no_row()
    {
        using var screen = new TaskListScreen(Services());
        screen.TypeInSearch("@");

        screen.Press(Key.Down);

        Assert.Empty(screen.SelectedTitles);
        Assert.NotEmpty(screen.ShownAliases);
        Assert.True(screen.SearchHasFocus);
    }

    [WpfFact]
    public void Esc_with_the_at_list_open_closes_only_the_list_then_the_next_Esc_empties_the_search_then_the_next_closes_Task_View()
    {
        using var screen = new TaskListScreen(Services());
        screen.TypeInSearch("@an");

        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownAliases);
        Assert.Equal("@an", screen.SearchText);
        Assert.True(screen.View.IsOpen);

        screen.Press(Key.Escape);

        Assert.Equal("", screen.SearchText);
        Assert.True(screen.View.IsOpen);

        screen.PressToClose(Key.Escape);

        Assert.False(screen.View.IsOpen);
    }

    [WpfFact]
    public void A_search_that_names_a_contact_shows_the_tasks_again_when_contacts_change()
    {
        var services = Services();
        using var screen = new TaskListScreen(services);
        screen.TypeSearch("@newbie");
        Assert.Empty(screen.RowTitles);

        services.ChangeContacts(ContactFiles.Person("ana", "Ana Pop", ("alias", "newbie")));

        Assert.Equal(["Call Ana Pop today"], screen.RowTitles);
    }

    [WpfFact]
    public async Task Resting_on_a_mention_in_a_rows_title_has_the_card_of_its_contact()
    {
        using var screen = new TaskListScreen(Services());

        var card = await screen.CardAt("Call Ana Pop today", "Ana Pop");

        Assert.NotNull(card);
        Assert.Contains("Ana Pop", EditorFields.TextsOfCard(card));
    }

    [WpfFact]
    public async Task Resting_on_the_text_around_a_mention_in_a_rows_title_has_no_card()
    {
        using var screen = new TaskListScreen(Services());

        Assert.Null(await screen.CardAt("Call Ana Pop today", "Call"));
    }

    [WpfFact]
    public async Task Resting_on_a_mention_whose_contact_Contacts_cannot_find_has_no_card()
    {
        var services = Services();
        services.Add($"Call {Zed} today");
        using var screen = new TaskListScreen(services);

        Assert.Null(await screen.CardAt("Call Zed Old today", "Zed Old"));
    }

    [WpfFact]
    public async Task Resting_on_a_mention_in_the_selected_tasks_description_has_the_card_of_its_contact()
    {
        var services = Services();
        services.Add("Plan", $"Ask {Ana} first");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Plan");

        var card = await screen.DescriptionCardAt(0);

        Assert.NotNull(card);
        Assert.Contains("Ana Pop", EditorFields.TextsOfCard(card));
    }

    [WpfFact]
    public async Task Resting_on_a_mention_in_the_edits_Preview_has_the_card_of_its_contact()
    {
        var services = Services();
        services.Add("Plan", $"Ask {Ana} first");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Plan", Key.F2);
        screen.ClickTab("Preview");

        var card = await screen.DescriptionCardAt(0);

        Assert.NotNull(card);
        Assert.Contains("Ana Pop", EditorFields.TextsOfCard(card));
    }

    [WpfFact]
    public async Task Resting_on_a_mention_in_a_description_whose_contact_Contacts_cannot_find_has_no_card()
    {
        var services = Services();
        services.Add("Plan", $"Ask {Zed} first");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Plan");

        Assert.Null(await screen.DescriptionCardAt(0));
    }

    [WpfFact]
    public async Task Resting_on_a_chip_in_the_row_title_being_edited_has_the_card_of_its_contact()
    {
        using var screen = new TaskListScreen(Services());
        screen.PressOn("Call Ana Pop today", Key.LeftShift, Key.F2);
        var editor = (StyledTextEditor)screen.EditorTitle("Call Ana Pop today");

        var part = await editor.PartAt(EditorFields.InsideAt(editor, 7));

        Assert.NotNull(part);
        Assert.Contains("Ana Pop", EditorFields.TextsOfCard(part.Card));
    }

    [WpfFact]
    public async Task Resting_on_a_chip_in_the_description_being_edited_has_the_card_of_its_contact()
    {
        var services = Services();
        services.Add("Plan", $"Ask {Ana} first");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Plan", Key.F2);
        var editor = (StyledTextEditor)screen.EditedDescription;

        var part = await editor.PartAt(EditorFields.InsideAt(editor, 7));

        Assert.NotNull(part);
        Assert.Contains("Ana Pop", EditorFields.TextsOfCard(part.Card));
    }
}
