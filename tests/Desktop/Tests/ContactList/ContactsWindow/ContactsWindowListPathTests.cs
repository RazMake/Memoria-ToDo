using System.Windows.Input;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactsWindow;

/// <summary>
/// The Contacts window's list along the paths a user takes: reading and searching it, moving through it with the keyboard, and dragging a group.
/// Real Contacts over an in-memory clone. The rules, edge cases and errors are in the unit tests.
/// </summary>
[UX("ContactsWindow")]
[Collection(ScreenTests.Collection)]
public class ContactsWindowListPathTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    [WpfFact]
    public void Open_read_the_rows_pick_a_contact_follow_links_while_the_search_hides_them_and_search_again()
    {
        using var screen = new ContactsScreen(_services);

        // Opening shows the groups with their contacts and asks to pick one.
        Assert.Equal(["Me", "Team", "Ex-Team", "Friends", "Empty"], screen.GroupNames);
        Assert.Equal(["Ana Pop", "Bob Stan"], screen.RowNamesIn("Team"));
        Assert.Contains("Pick a contact to see the details.", screen.DetailsTexts);

        // A row shows the full name, then the alias in italics between brackets; a contact with no alias shows just the name.
        Assert.Equal("Ana Pop (anap)", screen.RowText("Ana Pop"));
        Assert.Equal("anap", screen.RowItalicText("Ana Pop"));
        Assert.Equal("Bob Stan", screen.RowText("Bob Stan"));
        Assert.Null(screen.RowItalicText("Bob Stan"));

        // Clicking a contact shows its details on the right.
        screen.ClickRow("Bob Stan");
        Assert.Equal(["Bob Stan"], screen.SelectedNames);
        Assert.Contains("Designer", screen.DetailsTexts);

        // The problems line and its list are in the help style, not the caution color.
        _services.Disk.Seed(_services.Clone, TestContactStore.Folder + "/broken.json", "not json");
        _services.Load();
        screen.UseTheme();
        var help = screen.ThemeColor("TextFillColorSecondaryBrush");
        var summary = screen.TextBlockStartingWith("1 contact file skipped");
        screen.ClickText("1 contact file skipped");
        var item = screen.TextBlockStartingWith("autocomplete/contacts/broken.json");
        Assert.Equal(help, ContactsScreen.ColorOf(summary));
        Assert.Equal(help, ContactsScreen.ColorOf(item));
        Assert.NotEqual(screen.ThemeColor("SystemFillColorCautionBrush"), ContactsScreen.ColorOf(summary));

        // Clicking a direct report the search hides empties the search box and lists everyone.
        screen.ClickRow("Ana Pop");
        screen.Search("pop");
        Assert.DoesNotContain("Bob Stan", screen.RowNames);
        screen.ClickText("Bob Stan");
        Assert.Equal("", screen.SearchText);
        Assert.Equal(["Bob Stan"], screen.SelectedNames);
        Assert.Contains("Cris Ilie", screen.RowNames);

        // The same for Reports to.
        screen.ClickRow("Ana Pop");
        screen.Search("anap");
        Assert.DoesNotContain("Razvan Popov", screen.RowNames);
        screen.ClickText("Razvan Popov");
        Assert.Equal("", screen.SearchText);
        Assert.Equal(["Razvan Popov"], screen.SelectedNames);

        // A search after such a click starts from an empty box and leaves the contacts that match.
        screen.Search("oradea");
        Assert.Equal("oradea", screen.SearchText);
        Assert.Equal(["Friends"], screen.GroupNames);
        Assert.Equal(["Dan Rus"], screen.RowNames);
    }

    [WpfFact]
    public void Move_through_the_list_with_Down_Tab_Shift_Tab_and_off_its_edges_to_the_search_box()
    {
        using var screen = new ContactsScreen(_services);

        // Down selects the next contact and keeps the focus on the list.
        screen.ClickRow("Ana Pop");
        screen.PressKey(Key.Down);
        Assert.Equal(["Bob Stan"], screen.SelectedNames);
        Assert.Equal("Bob Stan", screen.FocusedRowName);

        // Tab goes to the next group's Add button, then to its first contact and selects it.
        screen.ClickRow("Ana Pop");
        screen.PressTab();
        Assert.Equal("Add", screen.FocusedButtonLabelIn("Ex-Team"));
        screen.PressTab();
        Assert.Equal("Cris Ilie", screen.FocusedRowName);
        Assert.Equal(["Cris Ilie"], screen.SelectedNames);

        // Shift+Tab into a group with no selected contact lands on its first contact and selects it.
        screen.ClickRow("Cris Ilie");
        screen.PressShiftTab();
        screen.PressShiftTab();
        Assert.Equal("Ana Pop", screen.FocusedRowName);
        Assert.Equal(["Ana Pop"], screen.SelectedNames);

        // Shift+Tab into a group with a selected contact lands on it and keeps it selected.
        screen.ClickRow("Bob Stan");
        screen.PressTab();
        screen.PressShiftTab();
        Assert.Equal("Bob Stan", screen.FocusedRowName);
        Assert.Equal(["Bob Stan"], screen.SelectedNames);

        // Down on the last contact and Up on the first move the focus to the search box with all its text selected.
        screen.Search("pop");
        screen.ClickRow(screen.RowNames[^1]);
        screen.PressKey(Key.Down);
        Assert.True(screen.SearchHasFocus);
        Assert.Equal("pop", screen.SearchSelectedText);
        screen.ClickRow(screen.RowNames[0]);
        screen.PressKey(Key.Up);
        Assert.True(screen.SearchHasFocus);
        Assert.Equal("pop", screen.SearchSelectedText);
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
