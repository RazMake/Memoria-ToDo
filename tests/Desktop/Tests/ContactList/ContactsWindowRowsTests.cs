using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>What a row of the Contacts list says, and how the keyboard moves through the list: Up and Down, the edges, and Tab.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ContactsWindowRowsTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    [WpfFact]
    public void A_row_shows_the_full_name_then_the_alias_in_italics_between_brackets_and_no_title()
    {
        using var screen = new ContactsScreen(_services);

        Assert.Equal("Ana Pop (anap)", screen.RowText("Ana Pop"));
        Assert.Equal("anap", screen.RowItalicText("Ana Pop"));
    }

    [WpfFact]
    public void A_row_of_a_contact_with_no_alias_shows_just_the_full_name()
    {
        using var screen = new ContactsScreen(_services);

        Assert.Equal("Bob Stan", screen.RowText("Bob Stan"));
        Assert.Null(screen.RowItalicText("Bob Stan"));
    }

    [WpfFact]
    public void Down_after_clicking_a_contact_selects_the_next_contact_and_keeps_the_focus_on_the_list()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Ana Pop");

        screen.PressKey(Key.Down);

        Assert.Equal(["Bob Stan"], screen.SelectedNames);
        Assert.Equal("Bob Stan", screen.FocusedRowName);
    }

    [WpfFact]
    public void Down_on_the_last_contact_moves_the_focus_to_the_search_box_with_all_its_text_selected()
    {
        using var screen = new ContactsScreen(_services);
        screen.Search("pop");
        screen.ClickRow(screen.RowNames[^1]);

        screen.PressKey(Key.Down);

        Assert.True(screen.SearchHasFocus);
        Assert.Equal("pop", screen.SearchSelectedText);
    }

    [WpfFact]
    public void Up_on_the_first_contact_moves_the_focus_to_the_search_box_with_all_its_text_selected()
    {
        using var screen = new ContactsScreen(_services);
        screen.Search("pop");
        screen.ClickRow(screen.RowNames[0]);

        screen.PressKey(Key.Up);

        Assert.True(screen.SearchHasFocus);
        Assert.Equal("pop", screen.SearchSelectedText);
    }

    [WpfFact]
    public void Tab_from_a_contact_goes_to_the_next_groups_Add_button_then_to_its_first_contact_and_selects_it()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Ana Pop");

        screen.PressTab();
        Assert.Equal("Add", screen.FocusedButtonLabelIn("Ex-Team"));

        screen.PressTab();
        Assert.Equal("Cris Ilie", screen.FocusedRowName);
        Assert.Equal(["Cris Ilie"], screen.SelectedNames);
    }

    [WpfFact]
    public void Shift_Tab_into_a_group_with_no_selected_contact_lands_on_its_first_contact_and_selects_it()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Cris Ilie");

        screen.PressShiftTab();
        screen.PressShiftTab();

        Assert.Equal("Ana Pop", screen.FocusedRowName);
        Assert.Equal(["Ana Pop"], screen.SelectedNames);
    }

    [WpfFact]
    public void Shift_Tab_into_a_group_lands_on_its_selected_contact_and_keeps_it_selected()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Bob Stan");
        screen.PressTab();

        screen.PressShiftTab();

        Assert.Equal("Bob Stan", screen.FocusedRowName);
        Assert.Equal(["Bob Stan"], screen.SelectedNames);
    }

    [WpfFact]
    public void Clicking_another_contact_with_a_changed_form_open_and_choosing_Cancel_asks_once_and_keeps_the_form_and_the_selection()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Ana Pop");
        screen.ClickButton("Edit");
        screen.Type("title", "Lead");

        var asked = screen.QuestionsAskedWhile(() => screen.ClickRow("Bob Stan"), "Cancel");

        Assert.Equal(1, asked);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.Equal(["Ana Pop"], screen.SelectedNames);
    }

    [WpfFact]
    public void Tab_onto_another_contact_with_a_changed_form_open_and_choosing_Cancel_asks_once_and_returns_the_focus_to_the_selected_contact()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickRow("Dan Rus");
        screen.ClickButton("Edit");
        screen.Type("title", "Lead");
        screen.FocusRow("Dan Rus");

        var asked = screen.QuestionsAskedWhile(() =>
        {
            screen.PressShiftTab();
            screen.PressShiftTab();
        }, "Cancel");

        Assert.Equal(1, asked);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.Equal(["Dan Rus"], screen.SelectedNames);
        Assert.Equal("Dan Rus", screen.FocusedRowName);
    }
}
