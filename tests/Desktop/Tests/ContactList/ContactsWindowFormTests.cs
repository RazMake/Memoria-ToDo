using System.Windows.Controls;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>The form on the Contacts screen: what a date picker holds before it commits, a data type that stops loading, and a close that saves.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ContactsWindowFormTests
{
    private static readonly DateOnly Date = new(2025, 1, 31);

    private readonly ContactServices _services = ContactServices.Typical();

    private readonly ClosePrompt _prompt = new("Contacts");

    private static ContactsScreen EditingAna(ContactServices services)
    {
        var screen = new ContactsScreen(services);
        screen.ClickRow("Ana Pop");
        screen.ClickButton("Edit");
        return screen;
    }

    private int CloseAnsweringWith(ContactsScreen screen, string answer)
    {
        var window = screen.Window;
        return _prompt.Run(() =>
        {
            ClosePrompt.PressCloseButton(window);
            return _prompt.Shown;
        }, ClosePrompt.Click(answer));
    }

    [WpfFact]
    public void A_date_typed_in_the_date_pickers_text_and_not_yet_committed_is_saved_by_the_Save_keys()
    {
        using var screen = EditingAna(_services);

        screen.TypeDate("exitDate", Date);
        screen.PressSaveKeys();

        Assert.Equal(Date, _services.Member("team", "ana").ExitDate);
    }

    [WpfFact]
    public void A_date_typed_in_the_date_pickers_text_and_not_yet_committed_counts_as_a_change_for_the_close_prompt()
    {
        using var screen = EditingAna(_services);
        screen.TypeDate("exitDate", Date);

        CloseAnsweringWith(screen, "Cancel");

        Assert.Equal(1, _prompt.Shown);
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void A_key_field_whose_data_type_stops_loading_turns_read_only_with_its_stored_key_and_names_the_file_and_the_typed_values_stay()
    {
        using var screen = EditingAna(_services);
        screen.Type("title", "Lead");

        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, "not json");
        _services.Load();

        var field = Assert.IsType<TextBox>(screen.Field("careerPathKey"));
        Assert.True(field.IsReadOnly);
        Assert.Equal("se", field.Text);
        Assert.True(screen.ShowsText("careerPaths.json"));
        Assert.Equal("Lead", screen.FieldText("title"));
    }

    [WpfFact]
    public void A_key_field_whose_data_type_loads_again_offers_its_entries_in_the_drop_down()
    {
        using var screen = EditingAna(_services);
        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, "not json");
        _services.Load();
        Assert.IsType<TextBox>(screen.Field("careerPathKey"));

        _services.Disk.SeedDataTypes(_services.Clone);
        _services.Load();

        var field = Assert.IsType<ComboBox>(screen.Field("careerPathKey"));
        Assert.Equal(["", "Engineering Manager", "Software Engineer"], field.Items.Cast<string>());
        Assert.Equal("Software Engineer", field.SelectedItem);
        Assert.False(screen.ShowsText("careerPaths.json"));
    }

    [WpfFact]
    public void A_picked_key_that_a_reload_of_its_file_no_longer_lists_stays_selected_in_the_rebuilt_drop_down_marked_Not_in_the_list()
    {
        using var screen = EditingAna(_services);
        ((ComboBox)screen.Field("careerPathKey")).SelectedItem = "Engineering Manager";

        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, ContactFiles.ArrayOf(
            ContactFiles.Object(("id", "se"), ("name", "Software Engineer"))));
        _services.Load();

        var field = Assert.IsType<ComboBox>(screen.Field("careerPathKey"));
        Assert.Equal("em (Not in the list)", field.SelectedItem);
        Assert.Contains("em (Not in the list)", field.Items.Cast<string>());
    }

    [WpfFact]
    public void Choosing_Save_on_the_close_prompt_when_the_save_fails_keeps_Contacts_open_and_says_why_on_the_message_line()
    {
        using var screen = EditingAna(_services);
        screen.Type("title", "Lead");
        _services.Disk.FailWritesUnder(_services.Clone);

        CloseAnsweringWith(screen, "Save");

        Assert.Equal(1, _prompt.Shown);
        Assert.True(screen.View.IsOpen);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        Assert.Equal("Lead", screen.FieldText("title"));
    }

    [WpfFact]
    public void A_save_the_store_refuses_is_logged_as_an_error_with_the_exception_through_the_windows_logger()
    {
        using var screen = EditingAna(_services);
        screen.Type("title", "Lead");
        _services.Disk.FailWritesUnder(_services.Clone);

        screen.PressSaveKeys();

        Assert.Contains(_services.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.Exception is not null);
    }

    [WpfFact]
    public void Choosing_Save_on_the_close_prompt_when_a_field_check_fails_keeps_Contacts_open_and_shows_the_problem_under_the_field()
    {
        using var screen = new ContactsScreen(_services);
        screen.ClickButton("Add", inGroup: "Friends");
        screen.Type("id", "eva");

        CloseAnsweringWith(screen, "Save");

        Assert.Equal(1, _prompt.Shown);
        Assert.True(screen.View.IsOpen);
        Assert.True(screen.ShowsText("Write the full name."));
        Assert.DoesNotContain(_services.Group("friends").Members, member => member.Id == "eva");
    }
}
