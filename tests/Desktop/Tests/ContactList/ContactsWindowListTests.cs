using System.Windows.Media;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>The list and its menus on the Contacts screen: a click that empties the search, Add and Delete on a row's menu, the user's own group, and the problems line's look.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ContactsWindowListTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private readonly ClosePrompt _prompt = new("Contacts");

    [WpfFact]
    public void Clicking_a_direct_report_the_search_hides_empties_the_search_box_and_lists_everyone()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Ana Pop");
        screen.Search("pop");
        Assert.DoesNotContain("Bob Stan", screen.RowNames);

        screen.ClickText("Bob Stan");

        Assert.Equal("", screen.SearchText);
        Assert.Equal(["Bob Stan"], screen.SelectedNames);
        Assert.Contains("Cris Ilie", screen.RowNames);
    }

    [WpfFact]
    public void Clicking_Reports_to_the_search_hides_empties_the_search_box_and_lists_everyone()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Ana Pop");
        screen.Search("anap");
        Assert.DoesNotContain("Razvan Popov", screen.RowNames);

        screen.ClickText("Razvan Popov");

        Assert.Equal("", screen.SearchText);
        Assert.Equal(["Razvan Popov"], screen.SelectedNames);
    }

    [WpfFact]
    public void Typing_a_search_after_such_a_click_starts_from_an_empty_box()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Ana Pop");
        screen.Search("pop");
        screen.ClickText("Bob Stan");

        screen.Search("oradea");

        Assert.Equal("oradea", screen.SearchText);
        Assert.Equal(["Dan Rus"], screen.RowNames);
    }

    [WpfFact]
    public void A_contacts_right_click_menu_offers_Add_and_Delete_in_another_group()
    {
        using var screen = new ContactsScreen(_services);

        Assert.Equal(["Add", "Delete"], screen.RowMenu("Bob Stan").Order());
        Assert.Equal(["Add", "Delete"], screen.RowMenu("Dan Rus").Order());
    }

    [WpfFact]
    public void The_users_own_contacts_right_click_menu_offers_neither_Add_nor_Delete()
    {
        using var screen = new ContactsScreen(_services);

        Assert.False(screen.OpensRowMenu("Razvan Popov"));
    }

    [WpfFact]
    public void Add_on_a_contacts_menu_opens_an_empty_form_for_that_contacts_group()
    {
        using var screen = new ContactsScreen(_services);

        screen.ChooseFromRowMenu("Dan Rus", "Add");

        Assert.True(screen.ShowsText("New contact in Friends"));
        Assert.Equal("", screen.FieldText("id"));
    }

    [WpfFact]
    public void Add_on_a_contacts_menu_with_a_changed_form_open_asks_first_and_Cancel_keeps_the_form()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Bob Stan");
        screen.ClickButton("Edit");
        screen.Type("title", "Lead");

        _prompt.Run(() =>
        {
            // The right-click on the contact being edited selects nothing new, so only Add asks.
            screen.ChooseFromRowMenu("Bob Stan", "Add");
            return 0;
        }, ClosePrompt.Click("Cancel"));

        Assert.Equal(1, _prompt.Shown);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.True(screen.ShowsText("Edit Bob Stan"));
    }

    [WpfFact]
    public void A_group_in_a_file_named_Me_json_is_the_users_own_group_with_no_Add_on_its_heading_or_menu_and_no_Delete()
    {
        var services = ContactServices.OwnFileNamed("Me.json");
        using var screen = new ContactsScreen(services);

        Assert.False(screen.HeadingHasButton("Me", "Add"));
        Assert.True(screen.HeadingHasButton("Friends", "Add"));
        Assert.False(screen.OpensRowMenu("Razvan Popov"));

        screen.ClickRow("Razvan Popov");

        Assert.False(screen.HasButton("Delete"));
        Assert.True(screen.HasButton("Edit"));
    }

    [WpfFact]
    public void The_problems_line_and_its_list_are_in_the_help_style_not_the_caution_color()
    {
        _services.Disk.Seed(_services.Clone, TestContactStore.Folder + "/broken.json", "not json");
        _services.Load();
        using var screen = new ContactsScreen(_services);
        screen.UseTheme();
        var help = screen.ThemeColor("TextFillColorSecondaryBrush");

        var summary = screen.TextBlockStartingWith("1 contact file skipped");
        screen.ClickText("1 contact file skipped");
        var item = screen.TextBlockStartingWith("autocomplete/contacts/broken.json");

        Assert.Equal(help, ContactsScreen.ColorOf(summary));
        Assert.Equal(help, ContactsScreen.ColorOf(item));
        Assert.NotEqual(screen.ThemeColor("SystemFillColorCautionBrush"), ContactsScreen.ColorOf(summary));
    }
}
