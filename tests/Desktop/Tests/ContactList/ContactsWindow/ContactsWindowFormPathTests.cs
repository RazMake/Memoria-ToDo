using System.Windows.Controls;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactsWindow;

/// <summary>
/// The Contacts window's form along the paths a user takes: editing, adding and deleting a contact, a save that fails, and a data type file that
/// stops loading and loads again. Real Contacts over an in-memory clone. The rules, edge cases and errors are in the unit tests.
/// </summary>
[UX("ContactsWindow")]
[Collection(ScreenTests.Collection)]
public class ContactsWindowFormPathTests
{
    private static readonly DateOnly Date = new(2025, 1, 31);

    private readonly ContactServices _services = ContactServices.Typical();

    private static ContactsScreen EditingAna(ContactServices services)
    {
        var screen = new ContactsScreen(services);
        screen.ClickRow("Ana Pop");
        screen.ClickButton("Edit");
        return screen;
    }

    private static int AskedWhileClosing(ContactsScreen screen, string answer) =>
        screen.QuestionsAskedWhile(() => ClosePrompt.PressCloseButton(screen.Window), answer);

    [WpfFact]
    public void Edit_contacts_and_save_with_the_Save_keys_then_leave_a_changed_form_by_click_and_by_Tab()
    {
        using var screen = new ContactsScreen(_services);

        // A contact with no stored key lists only the entries and selects none; a typed title is saved and the contact shows selected.
        screen.ClickRow("Bob Stan");
        screen.ClickButton("Edit");
        var keyField = Assert.IsType<ComboBox>(screen.Field("careerPathKey"));
        Assert.Equal(["Engineering Manager", "Software Engineer"], keyField.Items.Cast<string>());
        Assert.Null(keyField.SelectedItem);
        screen.Type("title", "Lead");
        screen.PressSaveKeys();
        Assert.Equal("Lead", _services.Member("team", "bob").Title);
        Assert.Equal(["Bob Stan"], screen.SelectedNames);
        Assert.Contains("Lead", screen.DetailsTexts);

        // A date typed in the date picker's text and not yet committed, and a picked entry of a key field, are saved by the Save keys.
        screen.ClickRow("Ana Pop");
        screen.ClickButton("Edit");
        screen.TypeDate("exitDate", Date);
        ((ComboBox)screen.Field("careerPathKey")).SelectedItem = "Engineering Manager";
        screen.PressSaveKeys();
        Assert.Equal(Date, _services.Member("team", "ana").ExitDate);
        Assert.Equal("em", _services.Member("team", "ana").CareerPathKey);

        // Clicking another contact with a changed form open and choosing Cancel asks once and keeps the form and the selection.
        screen.ClickRow("Ana Pop");
        screen.ClickButton("Edit");
        screen.Type("title", "Lead");
        var askedByClick = screen.QuestionsAskedWhile(() => screen.ClickRow("Bob Stan"), "Cancel");
        Assert.Equal(1, askedByClick);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.Equal(["Ana Pop"], screen.SelectedNames);

        // Tabbing onto another contact with a changed form open and choosing Cancel asks once and returns the focus to the selected contact.
        screen.QuestionsAskedWhile(() => screen.ClickRow("Dan Rus"), "Discard");
        screen.ClickButton("Edit");
        screen.Type("title", "Lead");
        screen.FocusRow("Dan Rus");
        var askedByTab = screen.QuestionsAskedWhile(() =>
        {
            screen.PressShiftTab();
            screen.PressShiftTab();
        }, "Cancel");
        Assert.Equal(1, askedByTab);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.Equal(["Dan Rus"], screen.SelectedNames);
        Assert.Equal("Dan Rus", screen.FocusedRowName);
    }

    [WpfFact]
    public void Add_a_contact_from_a_group_heading_then_from_a_contacts_menu_also_with_a_changed_form_open()
    {
        using var screen = new ContactsScreen(_services);

        // Add on a group's heading opens a form that saves the new contact into the group and selects it.
        screen.ClickButton("Add", inGroup: "Friends");
        screen.Type("id", "eva");
        screen.Type("fullName", "Eva Marin");
        screen.PressSaveKeys();
        Assert.Equal(["Dan Rus", "Eva Marin"], screen.RowNamesIn("Friends"));
        Assert.Equal(["Eva Marin"], screen.SelectedNames);

        // A contact's right-click menu offers Add and Delete in another group.
        Assert.Equal(["Add", "Delete"], screen.RowMenu("Bob Stan").Order());
        Assert.Equal(["Add", "Delete"], screen.RowMenu("Dan Rus").Order());

        // Add on a contact's menu opens an empty form for that contact's group.
        screen.ChooseFromRowMenu("Dan Rus", "Add");
        Assert.True(screen.ShowsText("New contact in Friends"));
        Assert.Equal("", screen.FieldText("id"));

        // With a changed form open it asks first, and Cancel keeps the form.
        screen.ClickRow("Bob Stan");
        screen.ClickButton("Edit");
        screen.Type("title", "Lead");
        var asked = screen.QuestionsAskedWhile(() => screen.ChooseFromRowMenu("Bob Stan", "Add"), "Cancel");
        Assert.Equal(1, asked);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.True(screen.ShowsText("Edit Bob Stan"));
    }

    [WpfFact]
    public void Delete_a_contact_after_confirming_and_the_users_own_contact_has_no_Delete_Add_or_menu()
    {
        using (var screen = new ContactsScreen(_services))
        {
            screen.ClickRow("Cris Ilie");

            screen.DeleteAnsweringWith("Delete");

            Assert.DoesNotContain("Cris Ilie", screen.RowNames);
            Assert.Empty(_services.Group("ex-team").Members);
            Assert.False(screen.OpensRowMenu("Razvan Popov"));
        }

        // The user's own group is the file named Me.json: no Add on its heading or menu, and no Delete.
        using var own = new ContactsScreen(ContactServices.OwnFileNamed("Me.json"));
        Assert.False(own.HeadingHasButton("Me", "Add"));
        Assert.True(own.HeadingHasButton("Friends", "Add"));
        Assert.False(own.OpensRowMenu("Razvan Popov"));
        own.ClickRow("Razvan Popov");
        Assert.False(own.HasButton("Delete"));
        Assert.True(own.HasButton("Edit"));
    }

    [WpfFact]
    public void A_save_that_fails_keeps_Contacts_open_says_why_and_is_logged_and_a_field_problem_shows_under_its_field()
    {
        using var screen = EditingAna(_services);

        // A date typed and not yet committed counts as a change for the close prompt.
        screen.TypeDate("exitDate", Date);
        Assert.Equal(1, AskedWhileClosing(screen, "Cancel"));
        Assert.True(screen.View.IsOpen);

        // Choosing Save on the close prompt when the save fails keeps Contacts open and says why on the message line.
        screen.Type("title", "Lead");
        _services.Disk.FailWritesUnder(_services.Clone);
        Assert.Equal(1, AskedWhileClosing(screen, "Save"));
        Assert.True(screen.View.IsOpen);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        Assert.Equal("Lead", screen.FieldText("title"));

        // A save the store refuses is logged as an error with the exception, through the window's logger.
        var loggedBefore = _services.Log.Events.Count(IsErrorWithException);
        screen.PressSaveKeys();
        Assert.True(_services.Log.Events.Count(IsErrorWithException) > loggedBefore);

        // Choosing Save on the close prompt when a field check fails keeps Contacts open and shows the problem under the field.
        screen.QuestionsAskedWhile(() => screen.ClickButton("Add", inGroup: "Friends"), "Discard");
        screen.Type("id", "eva");
        Assert.Equal(1, AskedWhileClosing(screen, "Save"));
        Assert.True(screen.View.IsOpen);
        Assert.True(screen.ShowsText("Write the full name."));
        Assert.DoesNotContain(_services.Group("friends").Members, member => member.Id == "eva");
    }

    [WpfFact]
    public void A_data_type_file_that_stops_loading_and_loads_again_while_the_form_is_open()
    {
        using var screen = EditingAna(_services);
        screen.Type("title", "Lead");

        // The key field turns read-only with its stored key and names the file; the typed values stay.
        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, "not json");
        _services.Load();
        var readOnly = Assert.IsType<TextBox>(screen.Field("careerPathKey"));
        Assert.True(readOnly.IsReadOnly);
        Assert.Equal("se", readOnly.Text);
        Assert.True(screen.ShowsText("careerPaths.json"));
        Assert.Equal("Lead", screen.FieldText("title"));

        // When the file loads again the drop-down offers its entries.
        _services.Disk.SeedDataTypes(_services.Clone);
        _services.Load();
        var dropDown = Assert.IsType<ComboBox>(screen.Field("careerPathKey"));
        Assert.Equal(["Engineering Manager", "Software Engineer"], dropDown.Items.Cast<string>());
        Assert.Equal("Software Engineer", dropDown.SelectedItem);
        Assert.False(screen.ShowsText("careerPaths.json"));

        // A picked key that a reload of its file no longer lists leaves the rebuilt drop-down with nothing selected, and the save is rejected.
        dropDown.SelectedItem = "Engineering Manager";
        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, ContactFiles.DataType("careerPaths",
            ContactFiles.Object(("id", "se"), ("name", "Software Engineer"))));
        _services.Load();
        var rebuilt = Assert.IsType<ComboBox>(screen.Field("careerPathKey"));
        Assert.Equal(["Software Engineer"], rebuilt.Items.Cast<string>());
        Assert.Null(rebuilt.SelectedItem);
        screen.PressSaveKeys();
        Assert.True(screen.HasButton("Save"));
        Assert.True(screen.ShowsText("'em' isn't listed"));
        Assert.Equal("se", _services.Member("team", "ana").CareerPathKey);
    }

    private static bool IsErrorWithException(LogEvent entry) => entry.Level == LogEventLevel.Error && entry.Exception is not null;
}
