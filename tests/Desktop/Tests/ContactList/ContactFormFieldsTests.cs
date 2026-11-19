using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>The form's own state and checks: which fields it shows, its drop-downs, what it finds wrong by itself, and the contact it would save.</summary>
[Unit]
public class ContactFormFieldsTests
{
    private const string TimeMessage = "Write the time as HH:mm, such as 09:30.";

    private static readonly string[] CommonFields =
        ["id", "alias", "fullName", "firstName", "title", "pronounsKey", "careerPathKey", "reportsTo"];

    private static readonly string[] TeamOnlyFields =
        ["location", "levelId", "workingType", "dayStartTime", "levelStartDate", "teamMemberSince", "exitDate"];

    private static readonly string[] TeamFormOrder =
    [
        "id", "alias", "fullName", "firstName", "title", "location", "levelId", "workingType", "pronounsKey", "careerPathKey",
        "reportsTo", "dayStartTime", "levelStartDate", "teamMemberSince", "exitDate",
    ];

    private static readonly string[] OtherFormOrder =
        ["id", "alias", "fullName", "firstName", "title", "pronounsKey", "careerPathKey", "reportsTo"];

    private readonly ContactServices _services = ContactServices.Typical();

    private ContactForm Edit(string group, string id) => ContactForm.Edit(_services.Contacts, group, _services.Member(group, id));

    private ContactForm Add(string group) => ContactForm.Add(_services.Contacts, group);

    [Fact]
    public void A_team_group_form_shows_every_field()
    {
        var form = Edit("team", "ana");

        Assert.Equal(CommonFields.Concat(TeamOnlyFields).Order(), form.FieldNames.Order());
    }

    [Fact]
    public void A_team_group_form_shows_the_fields_in_the_order_of_Contacts_field_table()
    {
        Assert.Equal(TeamFormOrder, Edit("team", "ana").FieldNames);
    }

    [Fact]
    public void Another_groups_form_shows_its_fields_in_the_order_of_Contacts_field_table()
    {
        Assert.Equal(OtherFormOrder, Edit("friends", "dan").FieldNames);
    }

    [Fact]
    public void An_ex_team_group_form_shows_every_field_too()
    {
        Assert.Equal(CommonFields.Concat(TeamOnlyFields).Order(), Edit("ex-team", "cris").FieldNames.Order());
    }

    [Fact]
    public void Another_groups_form_leaves_out_the_team_fields()
    {
        Assert.Equal(CommonFields.Order(), Edit("friends", "dan").FieldNames.Order());
    }

    [Fact]
    public void The_users_own_group_form_leaves_out_the_team_fields()
    {
        Assert.Equal(CommonFields.Order(), Edit("me", "razvanp").FieldNames.Order());
    }

    [Fact]
    public void An_edit_form_starts_with_the_contacts_values()
    {
        var form = Edit("team", "ana");

        Assert.Equal(("ana", "Ana Pop", "anap", "Engineer", "Cluj", "razvanp", "she", "se"),
            (form["id"], form["fullName"], form["alias"], form["title"], form["location"], form["reportsTo"], form["pronounsKey"], form["careerPathKey"]));
    }

    [Fact]
    public void A_missing_value_starts_as_empty_text()
    {
        Assert.Equal("", Edit("team", "bob")["alias"]);
    }

    [Fact]
    public void A_stored_time_shows_as_HH_mm()
    {
        Assert.Equal("09:30", Edit("team", "ana")["dayStartTime"]);
    }

    [Fact]
    public void A_stored_date_is_the_dates_value()
    {
        Assert.Equal(new DateOnly(2024, 3, 14), Edit("team", "ana").DateOf("levelStartDate"));
        Assert.Null(Edit("team", "ana").DateOf("exitDate"));
    }

    [Fact]
    public void An_existing_contacts_ID_is_read_only_and_its_other_fields_are_not()
    {
        var form = Edit("team", "ana");

        Assert.True(form.IsReadOnly("id"));
        Assert.False(form.IsReadOnly("alias"));
        Assert.False(form.IsReadOnly("fullName"));
    }

    [Fact]
    public void A_new_form_starts_empty_for_the_group_and_its_ID_can_be_typed()
    {
        var form = Add("team");

        Assert.True(form.IsNew);
        Assert.Equal("team", form.GroupName);
        Assert.False(form.IsReadOnly("id"));
        Assert.Equal("", form["id"]);
        Assert.Equal("", form["fullName"]);
    }

    [Fact]
    public void An_edit_form_is_not_new()
    {
        Assert.False(Edit("team", "ana").IsNew);
    }

    [Fact]
    public void A_form_just_opened_has_no_changes()
    {
        Assert.False(Edit("team", "ana").HasChanges);
        Assert.False(Add("team").HasChanges);
    }

    [Fact]
    public void Typing_in_a_field_makes_a_change_and_typing_it_back_takes_it_away()
    {
        var form = Edit("team", "ana");

        form["title"] = "Lead";
        Assert.True(form.HasChanges);

        form["title"] = "Engineer";
        Assert.False(form.HasChanges);
    }

    [Fact]
    public void Picking_a_date_is_a_change()
    {
        var form = Edit("team", "ana");

        form.SetDate("exitDate", new DateOnly(2025, 1, 31));

        Assert.True(form.HasChanges);
        Assert.Equal(new DateOnly(2025, 1, 31), form.DateOf("exitDate"));
    }

    [Fact]
    public void Typing_in_a_new_form_is_a_change()
    {
        var form = Add("friends");

        form["id"] = "eva";

        Assert.True(form.HasChanges);
    }

    [Fact]
    public void An_empty_ID_is_a_problem_under_its_field_and_stops_the_form()
    {
        var form = Add("friends");
        form["fullName"] = "Eva Marin";

        Assert.Equal("Write an ID.", form.ProblemOf("id"));
        Assert.True(form.HasProblems);
    }

    [Fact]
    public void An_empty_full_name_is_a_problem_under_its_field()
    {
        var form = Edit("team", "bob");

        form["fullName"] = "  ";

        Assert.Equal("Write the full name.", form.ProblemOf("fullName"));
        Assert.True(form.HasProblems);
    }

    [Fact]
    public void A_form_with_both_fields_filled_has_no_problems()
    {
        var form = Add("friends");
        form["id"] = "eva";
        form["fullName"] = "Eva Marin";

        Assert.False(form.HasProblems);
        Assert.Null(form.ProblemOf("id"));
        Assert.Null(form.ProblemOf("fullName"));
    }

    [Theory]
    [InlineData("dan")]
    [InlineData("ana")]
    [InlineData("ANA")]
    [InlineData("RazvanP")]
    [InlineData(" ana ")]
    public void A_new_contacts_ID_a_loaded_contact_holds_is_a_problem_under_the_ID_in_any_group_ignoring_case(string typed)
    {
        var form = Add("friends");
        form["fullName"] = "Another One";

        form["id"] = typed;

        Assert.Equal("Another contact has this ID.", form.ProblemOf("id"));
        Assert.True(form.HasProblems);
    }

    [Fact]
    public void A_new_contacts_ID_nobody_holds_is_no_problem()
    {
        var form = Add("friends");

        form["id"] = "eva";

        Assert.Null(form.ProblemOf("id"));
    }

    [Fact]
    public void An_existing_contact_keeps_its_own_ID_without_that_problem()
    {
        var form = Edit("friends", "dan");

        form["title"] = "Friend";

        Assert.Null(form.ProblemOf("id"));
        Assert.False(form.HasProblems);
    }

    [Fact]
    public void A_key_field_whose_data_type_stops_loading_while_the_form_is_open_turns_read_only_showing_its_stored_key_and_naming_the_file()
    {
        var form = Edit("team", "ana");
        form["title"] = "Lead";

        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, "not json");
        _services.Load();

        Assert.True(form.IsReadOnly("careerPathKey"));
        Assert.Contains("careerPaths.json", form.DataTypeProblem("careerPathKey"));
        Assert.Equal("se", form["careerPathKey"]);
        Assert.Equal("Lead", form["title"]);
        Assert.Null(Record.Exception(() => form.ChoicesOf("careerPathKey")));
        Assert.Equal("se", form.ToContact().CareerPathKey);
    }

    [Fact]
    public void A_key_field_whose_data_type_loads_again_offers_its_entries_again()
    {
        var form = Edit("team", "ana");
        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, "not json");
        _services.Load();
        _services.Disk.SeedDataTypes(_services.Clone);
        _services.Load();

        Assert.False(form.IsReadOnly("careerPathKey"));
        Assert.Null(form.DataTypeProblem("careerPathKey"));
        Assert.Equal(["em", "se"], form.ChoicesOf("careerPathKey").Select(choice => choice.Key));
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("nine")]
    [InlineData("9:61")]
    public void A_time_that_does_not_read_is_a_problem_in_the_forms_own_words(string typed)
    {
        var form = Edit("team", "bob");

        form["dayStartTime"] = typed;

        Assert.Equal(TimeMessage, form.ProblemOf("dayStartTime"));
        Assert.True(form.HasProblems);
    }

    [Theory]
    [InlineData("09:30")]
    [InlineData("9:30")]
    public void A_time_Contacts_reads_is_no_problem(string typed)
    {
        var form = Edit("team", "bob");

        form["dayStartTime"] = typed;

        Assert.Null(form.ProblemOf("dayStartTime"));
        Assert.False(form.HasProblems);
    }

    [Fact]
    public void An_empty_time_is_no_problem_since_the_field_may_be_missing()
    {
        var form = Edit("team", "ana");

        form["dayStartTime"] = "";

        Assert.Null(form.ProblemOf("dayStartTime"));
    }

    [Fact]
    public void The_form_makes_no_check_of_the_other_fields()
    {
        var form = Edit("team", "bob");

        form["alias"] = "has spaces, and anything";
        form["reportsTo"] = "nobody at all";
        form["location"] = "";

        Assert.False(form.HasProblems);
    }

    [Fact]
    public void A_key_fields_choices_are_its_data_types_entries_shown_by_name_or_ID()
    {
        var form = Edit("team", "ana");

        Assert.Equal([new FormChoice("em", "Engineering Manager", null), new FormChoice("se", "Software Engineer", null)], form.ChoicesOf("careerPathKey"));
        Assert.Equal([new FormChoice("he", "he", null), new FormChoice("she", "she", null)], form.ChoicesOf("pronounsKey"));
    }

    [Fact]
    public void A_stored_key_the_file_does_not_list_shows_first_as_written_and_marked()
    {
        var form = ContactForm.Edit(_services.Contacts, "team", _services.Member("team", "bob") with { CareerPathKey = "zz" });

        var choices = form.ChoicesOf("careerPathKey");

        Assert.Equal(new FormChoice("zz", "zz", "Not in the list"), choices[0]);
        Assert.Equal(["zz", "em", "se"], choices.Select(choice => choice.Key));
        Assert.Equal("zz", form["careerPathKey"]);
    }

    [Fact]
    public void A_stored_key_the_file_does_not_list_stays_in_the_choices_after_another_is_picked()
    {
        var form = ContactForm.Edit(_services.Contacts, "team", _services.Member("team", "bob") with { CareerPathKey = "zz" });

        form["careerPathKey"] = "em";
        var choices = form.ChoicesOf("careerPathKey");

        Assert.Equal(new FormChoice("zz", "zz", "Not in the list"), choices[0]);
        Assert.Equal(["zz", "em", "se"], choices.Select(choice => choice.Key));
        Assert.Equal("em", form["careerPathKey"]);
    }

    [Fact]
    public void A_picked_key_that_a_reload_of_the_data_type_file_no_longer_lists_shows_in_the_choices_marked_and_stays_the_value()
    {
        var form = Edit("team", "ana");
        form["careerPathKey"] = "em";

        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, ContactFiles.ArrayOf(
            ContactFiles.Object(("id", "se"), ("name", "Software Engineer"))));
        _services.Load();
        var choices = form.ChoicesOf("careerPathKey");

        Assert.Contains(new FormChoice("em", "em", "Not in the list"), choices);
        Assert.Contains(new FormChoice("se", "Software Engineer", null), choices);
        Assert.Equal(2, choices.Count);
        Assert.Equal("em", form["careerPathKey"]);
    }

    [Fact]
    public void Picking_the_unlisted_stored_key_back_sets_it_again()
    {
        var form = ContactForm.Edit(_services.Contacts, "team", _services.Member("team", "bob") with { CareerPathKey = "zz" });
        form["careerPathKey"] = "em";

        form["careerPathKey"] = "zz";

        Assert.Equal("zz", form["careerPathKey"]);
        Assert.False(form.HasChanges);
    }

    [Fact]
    public void Picking_a_listed_entry_sets_the_key_and_is_a_change()
    {
        var form = Edit("team", "ana");

        form["careerPathKey"] = "em";

        Assert.Equal("em", form["careerPathKey"]);
        Assert.True(form.HasChanges);
    }

    [Fact]
    public void A_key_field_with_a_loaded_data_type_can_be_picked_and_has_no_data_type_problem()
    {
        var form = Edit("team", "ana");

        Assert.False(form.IsReadOnly("careerPathKey"));
        Assert.Null(form.DataTypeProblem("careerPathKey"));
    }

    [Fact]
    public void A_key_field_whose_data_type_is_not_loaded_is_read_only_and_names_the_file_to_fix()
    {
        var services = new ContactServices();
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("bob", "Bob Stan", ("careerPathKey", "se")));
        services.Disk.Seed(services.Clone, TestContactStore.CareerPathsFile, "not json");
        services.Load();

        var form = ContactForm.Edit(services.Contacts, "team", services.Member("team", "bob"));

        Assert.True(form.IsReadOnly("careerPathKey"));
        Assert.Contains("careerPaths.json", form.DataTypeProblem("careerPathKey"));
        Assert.Equal("se", form["careerPathKey"]);
    }

    [Fact]
    public void A_form_on_a_contact_with_an_unloaded_key_still_saves_the_key_as_stored()
    {
        var services = new ContactServices();
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("bob", "Bob Stan", ("careerPathKey", "se")));
        services.Disk.Seed(services.Clone, TestContactStore.CareerPathsFile, "not json");
        services.Load();
        var form = ContactForm.Edit(services.Contacts, "team", services.Member("team", "bob"));

        form["title"] = "Lead";

        Assert.Equal("se", form.ToContact().CareerPathKey);
    }

    [Fact]
    public void An_unchanged_form_makes_the_same_contact()
    {
        var ana = _services.Member("team", "ana");

        Assert.Equal(ana, ContactForm.Edit(_services.Contacts, "team", ana).ToContact());
    }

    [Fact]
    public void Emptied_fields_become_missing()
    {
        var form = Edit("team", "ana");

        form["alias"] = "";
        form["dayStartTime"] = "";
        form.SetDate("levelStartDate", null);

        var contact = form.ToContact();

        Assert.Null(contact.Alias);
        Assert.Null(contact.DayStartTime);
        Assert.Null(contact.LevelStartDate);
    }

    [Fact]
    public void A_typed_time_and_a_picked_date_become_the_contacts_values()
    {
        var form = Edit("team", "bob");

        form["dayStartTime"] = "8:05";
        form.SetDate("teamMemberSince", new DateOnly(2023, 6, 1));

        var contact = form.ToContact();

        Assert.Equal(new TimeOnly(8, 5), contact.DayStartTime);
        Assert.Equal(new DateOnly(2023, 6, 1), contact.TeamMemberSince);
    }

    [Fact]
    public void A_new_forms_contact_has_the_typed_ID_and_name()
    {
        var form = Add("friends");
        form["id"] = "eva";
        form["fullName"] = "Eva Marin";

        Assert.Equal(new Contact("eva", "Eva Marin"), form.ToContact());
    }

    [Fact]
    public void Team_fields_the_form_does_not_show_stay_as_they_were()
    {
        var dan = _services.Member("friends", "dan");
        var form = ContactForm.Edit(_services.Contacts, "friends", dan);

        form["title"] = "Friend";

        Assert.Equal(dan with { Title = "Friend" }, form.ToContact());
        Assert.Equal("Oradea", form.ToContact().Location);
    }
}
