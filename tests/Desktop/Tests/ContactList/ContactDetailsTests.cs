using System.Globalization;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>What the right side shows for the selected contact while no form is open: which fields, in what words, and where a click goes.</summary>
[Unit]
public class ContactDetailsTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private ContactDetails DetailsOf(string group, string id, ContactsModel? model = null)
    {
        model ??= _services.Model();
        model.Select(group, id);
        return model.Details!;
    }

    private static IReadOnlyList<string> Texts(ContactDetails details) =>
        [.. details.Fields.SelectMany(field => field.Values).Select(value => value.Text)];

    private static DetailField Field(ContactDetails details, string label) => details.Fields.Single(field => field.Label == label);

    [Fact]
    public void The_heading_is_the_full_name_with_the_groups_heading_under_it()
    {
        var details = DetailsOf("team", "bob");

        Assert.Equal(("Bob Stan", "Team"), (details.Heading, details.GroupHeading));
    }

    [Fact]
    public void Shows_the_full_name_under_its_plain_label()
    {
        var details = DetailsOf("team", "bob");

        Assert.Equal("Bob Stan", Assert.Single(Field(details, "Full name").Values).Text);
    }

    [Fact]
    public void Shows_each_field_the_contact_has_and_leaves_out_the_missing_ones()
    {
        var details = DetailsOf("team", "bob");

        var own = details.Fields.Where(field => field.Label != "Direct reports").SelectMany(field => field.Values).Select(value => value.Text);

        Assert.Equal(new[] { "bob", "Bob Stan", "Designer", "Ana Pop" }.Order(), own.Order());
    }

    [Fact]
    public void Shows_team_fields_of_a_contact_in_a_team_group()
    {
        var texts = Texts(DetailsOf("team", "ana"));

        Assert.Contains("Cluj", texts);
    }

    [Fact]
    public void Hides_team_fields_of_a_contact_in_another_group_though_the_file_holds_them()
    {
        var texts = Texts(DetailsOf("friends", "dan"));

        Assert.DoesNotContain("Oradea", texts);
        Assert.Contains("Neighbor", texts);
    }

    [Fact]
    public void Reports_to_shows_the_managers_full_name_and_a_click_selects_the_manager()
    {
        var value = Assert.Single(Field(DetailsOf("team", "bob"), "Reports to").Values);

        Assert.Equal("Ana Pop", value.Text);
        Assert.Equal(new ContactRef("team", "ana"), value.Target);
    }

    [Fact]
    public void Reports_to_finds_the_manager_by_ID_too_in_another_group()
    {
        var value = Assert.Single(Field(DetailsOf("team", "ana"), "Reports to").Values);

        Assert.Equal("Razvan Popov", value.Text);
        Assert.Equal(new ContactRef("me", "razvanp"), value.Target);
    }

    [Fact]
    public void Names_every_known_field_in_plain_words()
    {
        _services.Contacts.Save("team", _services.Member("team", "ana") with
        {
            FirstName = "Ana", Nickname = "Annie", LevelId = "L5", WorkingType = "Remote",
            TeamMemberSince = new DateOnly(2023, 6, 1), ExitDate = new DateOnly(2026, 1, 31),
        });

        var labels = DetailsOf("team", "ana").Fields.Select(field => field.Label);

        Assert.Equal(
            [
                "ID", "Alias", "Full name", "First name", "Nickname", "Title", "Location", "Level", "Working type", "Pronouns", "Career path",
                "Reports to", "Direct reports", "Day start time", "Level start date", "Team member since", "Exit date",
            ],
            labels);
    }

    [Fact]
    public void Shows_the_stored_nickname_under_its_label_for_a_team_member()
    {
        _services.Contacts.Save("team", _services.Member("team", "ana") with { FirstName = "Ana", Nickname = "Annie" });

        Assert.Equal("Annie", Assert.Single(Field(DetailsOf("team", "ana"), "Nickname").Values).Text);
    }

    [Fact]
    public void Shows_the_stored_nickname_for_a_contact_in_another_group()
    {
        _services.Contacts.Save("friends", _services.Member("friends", "dan") with { Nickname = "Danny" });

        Assert.Equal("Danny", Assert.Single(Field(DetailsOf("friends", "dan"), "Nickname").Values).Text);
    }

    [Fact]
    public void Shows_the_nickname_right_after_the_first_name()
    {
        _services.Contacts.Save("team", _services.Member("team", "ana") with { FirstName = "Ana", Nickname = "Annie" });

        var labels = DetailsOf("team", "ana").Fields.Select(field => field.Label).ToList();

        Assert.Equal(labels.IndexOf("First name") + 1, labels.IndexOf("Nickname"));
    }

    [Fact]
    public void Shows_no_nickname_row_for_a_contact_with_no_stored_nickname_though_it_has_a_first_name()
    {
        _services.Contacts.Save("team", _services.Member("team", "ana") with { FirstName = "Ana" });

        var details = DetailsOf("team", "ana");

        Assert.DoesNotContain(details.Fields, field => field.Label == "Nickname");
        Assert.Equal("Ana", Assert.Single(Field(details, "First name").Values).Text);
        Assert.Single(Texts(details), "Ana");
    }

    [Fact]
    public void Shows_the_ID_under_its_label()
    {
        Assert.Equal("bob", Assert.Single(Field(DetailsOf("team", "bob"), "ID").Values).Text);
    }

    [Fact]
    public void Reports_to_shows_the_name_as_written_when_it_finds_nobody()
    {
        _services.Contacts.Save("friends", _services.Member("friends", "dan") with { ReportsTo = "ghost" });

        var value = Assert.Single(Field(DetailsOf("friends", "dan"), "Reports to").Values);

        Assert.Equal("ghost", value.Text);
        Assert.Null(value.Target);
    }

    [Fact]
    public void Direct_reports_lists_the_contacts_who_report_to_this_one_each_a_click_to_select()
    {
        var values = Field(DetailsOf("team", "ana"), "Direct reports").Values;

        var report = Assert.Single(values);
        Assert.Equal("Bob Stan", report.Text);
        Assert.Equal(new ContactRef("team", "bob"), report.Target);
    }

    [Fact]
    public void Direct_reports_are_listed_by_full_name()
    {
        _services.Contacts.Save("friends", _services.Member("friends", "dan") with { ReportsTo = "anap" });

        var values = Field(DetailsOf("team", "ana"), "Direct reports").Values;

        Assert.Equal(["Bob Stan", "Dan Rus"], values.Select(value => value.Text));
    }

    [Fact]
    public void A_click_on_a_manager_selects_that_contact()
    {
        var model = _services.Model();
        var target = Field(DetailsOf("team", "bob", model), "Reports to").Values.Single().Target!;

        model.Select(target.GroupName, target.Id);

        Assert.Equal("ana", model.Selected!.Contact.Id);
        Assert.Equal("Ana Pop", model.Details!.Heading);
    }

    [Fact]
    public void A_deleted_manager_leaves_the_name_as_written_for_those_who_reported_to_it()
    {
        _services.Contacts.Delete("team", "ana");

        var value = Assert.Single(Field(DetailsOf("team", "bob"), "Reports to").Values);

        Assert.Equal("anap", value.Text);
        Assert.Null(value.Target);
    }

    [Fact]
    public void A_time_shows_as_HH_mm_through_Contacts()
    {
        Assert.Contains("09:30", Texts(DetailsOf("team", "ana")));
    }

    [Fact]
    public void A_date_shows_in_the_users_short_date_format()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Contains("3/14/2024", Texts(DetailsOf("team", "ana")));

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Contains("14.03.2024", Texts(DetailsOf("team", "ana")));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void A_key_field_shows_its_entrys_name_or_its_ID_when_it_has_none()
    {
        var texts = Texts(DetailsOf("team", "ana"));

        Assert.Contains("Software Engineer", texts);
        Assert.Contains("she", texts);
        Assert.DoesNotContain("se", texts);
    }

    [Fact]
    public void A_key_the_data_type_doesnt_list_shows_as_written()
    {
        var services = new ContactServices();
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("bob", "Bob Stan", ("careerPathKey", "zz")));
        services.Disk.SeedDataTypes(services.Clone);
        services.Load();

        Assert.Contains("zz", Texts(DetailsOf("team", "bob", services.Model())));
    }

    [Fact]
    public void A_key_whose_data_type_file_is_missing_shows_as_written()
    {
        var services = new ContactServices();
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("bob", "Bob Stan", ("careerPathKey", "se")));
        services.Load();

        Assert.Contains("se", Texts(DetailsOf("team", "bob", services.Model())));
    }

    [Fact]
    public void The_users_own_contact_shows_its_details_like_any_other()
    {
        var details = DetailsOf("me", "razvanp");

        Assert.Equal(("Razvan Popov", "Me"), (details.Heading, details.GroupHeading));
        Assert.Contains("Director", Texts(details));
    }
}
