using System.Globalization;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactCard;

/// <summary>
/// What a contact card says for a mention's address: which fields show by default and while Ctrl is held, for each kind of contact,
/// how each value reads, the heading, and what the card keeps from the moment it opened. The card's content is
/// <see cref="ContactCardContent"/>, which <see cref="ContactList.ContactCard"/> shows; the details' fields are the same <see cref="DetailField"/>s.
/// </summary>
[Unit]
public class ContactCardContentTests
{
    private static readonly string[] CommonOfAna = ["Alias", "Title", "Pronouns", "Career path", "Reports to"];

    private readonly ContactServices _services = ContactServices.Typical();

    private ContactCardContent? Card(string id, bool ctrl = false) => ContactCardContent.Of(_services.Contacts, ContactAddress.Of(id), ctrl);

    private ContactCardContent Open(string id, bool ctrl = false) => Card(id, ctrl) ?? throw new InvalidOperationException($"No card for {id}.");

    private static IReadOnlyList<string> Labels(ContactCardContent card) => [.. card.Fields.Select(field => field.Label)];

    private static IReadOnlyList<string> Texts(ContactCardContent card) =>
        [.. card.Fields.SelectMany(field => field.Values).Select(value => value.Text)];

    private static DetailField Field(ContactCardContent card, string label) => card.Fields.Single(field => field.Label == label);

    private void GiveAnaEveryField() =>
        _services.Contacts.Save("team", _services.Member("team", "ana") with
        {
            FirstName = "Ana", Nickname = "Annie", LevelId = "L5", WorkingType = "Remote",
            TeamMemberSince = new DateOnly(2023, 6, 1), ExitDate = new DateOnly(2026, 1, 31),
        });

    // The heading

    [Fact]
    public void The_heading_is_the_full_name_with_the_groups_heading_under_it()
    {
        var card = Open("bob");

        Assert.Equal(("Bob Stan", "Team"), (card.Heading, card.GroupHeading));
    }

    [Fact]
    public void The_heading_of_the_users_own_contact_is_its_name_under_the_own_groups_heading()
    {
        var card = Open("razvanp");

        Assert.Equal(("Razvan Popov", "Me"), (card.Heading, card.GroupHeading));
    }

    [Fact]
    public void The_full_name_is_the_heading_and_not_a_field()
    {
        Assert.DoesNotContain("Full name", Labels(Open("ana", ctrl: true)));
    }

    // The default fields

    [Fact]
    public void Shows_by_default_the_common_fields_in_the_details_order()
    {
        Assert.Equal(CommonOfAna, Labels(Open("ana")));
    }

    [Fact]
    public void Shows_by_default_each_common_field_as_the_details_show_it()
    {
        Assert.Equal(["anap", "Engineer", "she", "Software Engineer", "Razvan Popov"], Texts(Open("ana")));
    }

    [Fact]
    public void Shows_by_default_no_team_field_even_for_a_contact_in_a_team_group()
    {
        var labels = Labels(Open("ana"));

        Assert.DoesNotContain("Location", labels);
        Assert.DoesNotContain("Day start time", labels);
        Assert.DoesNotContain("Level start date", labels);
    }

    [Fact]
    public void Shows_by_default_no_ID_first_name_or_direct_reports()
    {
        GiveAnaEveryField();

        var labels = Labels(Open("ana"));

        Assert.DoesNotContain("ID", labels);
        Assert.DoesNotContain("First name", labels);
        Assert.DoesNotContain("Direct reports", labels);
    }

    [Fact]
    public void Shows_by_default_no_nickname_though_the_contact_has_one()
    {
        GiveAnaEveryField();

        Assert.DoesNotContain("Nickname", Labels(Open("ana")));
    }

    [Fact]
    public void Shows_with_Ctrl_the_stored_nickname_right_after_the_first_name()
    {
        GiveAnaEveryField();

        var card = Open("ana", ctrl: true);
        var labels = Labels(card).ToList();

        Assert.Equal(labels.IndexOf("First name") + 1, labels.IndexOf("Nickname"));
        Assert.Equal("Annie", Assert.Single(Field(card, "Nickname").Values).Text);
    }

    [Fact]
    public void Shows_with_Ctrl_no_nickname_for_a_contact_with_a_first_name_and_no_stored_nickname()
    {
        _services.Contacts.Save("team", _services.Member("team", "ana") with { FirstName = "Ana" });

        var card = Open("ana", ctrl: true);

        Assert.Contains("First name", Labels(card));
        Assert.DoesNotContain("Nickname", Labels(card));
    }

    [Fact]
    public void Shows_the_same_common_fields_for_a_contact_in_another_group()
    {
        Assert.Equal(["Title"], Labels(Open("dan")));
    }

    [Fact]
    public void Hides_a_common_field_with_no_value()
    {
        Assert.Equal(["Title", "Reports to"], Labels(Open("bob")));
    }

    [Fact]
    public void A_contact_with_none_of_the_common_fields_shows_only_its_heading()
    {
        var card = Open("cris");

        Assert.Empty(card.Fields);
        Assert.Equal("Cris Ilie", card.Heading);
    }

    // Every field while Ctrl is held

    [Fact]
    public void Shows_with_Ctrl_every_field_the_details_show_for_a_team_member_in_the_details_order()
    {
        GiveAnaEveryField();

        Assert.Equal(
            [
                "ID", "Alias", "First name", "Nickname", "Title", "Location", "Level", "Working type", "Pronouns", "Career path",
                "Reports to", "Direct reports", "Day start time", "Level start date", "Team member since", "Exit date",
            ],
            Labels(Open("ana", ctrl: true)));
    }

    [Fact]
    public void Shows_with_Ctrl_a_team_members_team_fields_as_the_details_show_them()
    {
        GiveAnaEveryField();

        var card = Open("ana", ctrl: true);

        Assert.Equal("Cluj", Field(card, "Location").Values.Single().Text);
        Assert.Equal("Remote", Field(card, "Working type").Values.Single().Text);
        Assert.Equal("09:30", Field(card, "Day start time").Values.Single().Text);
    }

    [Fact]
    public void Shows_with_Ctrl_no_team_field_for_a_contact_in_another_group_though_its_file_holds_one()
    {
        var labels = Labels(Open("dan", ctrl: true));

        Assert.Equal(["ID", "Title"], labels);
    }

    [Fact]
    public void Shows_with_Ctrl_the_ID_of_a_contact_with_no_other_detail()
    {
        var card = Open("cris", ctrl: true);

        Assert.Equal(["ID"], Labels(card));
        Assert.Equal("cris", Texts(card).Single());
    }

    [Fact]
    public void Shows_with_Ctrl_only_the_team_fields_the_contact_has()
    {
        Assert.Equal(["ID", "Title", "Reports to"], Labels(Open("bob", ctrl: true)));
    }

    [Fact]
    public void Shows_with_Ctrl_the_users_own_contacts_details_including_who_reports_to_it()
    {
        var card = Open("razvanp", ctrl: true);

        Assert.Equal(["ID", "Title", "Direct reports"], Labels(card));
        Assert.Equal(["Ana Pop"], Field(card, "Direct reports").Values.Select(value => value.Text));
    }

    [Fact]
    public void Shows_with_Ctrl_every_common_field_the_default_shows_too()
    {
        Assert.Subset(Labels(Open("ana", ctrl: true)).ToHashSet(), CommonOfAna.ToHashSet());
    }

    // How a value reads

    [Fact]
    public void A_key_field_shows_its_entrys_name()
    {
        var card = Open("ana");

        Assert.Equal("Software Engineer", Field(card, "Career path").Values.Single().Text);
        Assert.DoesNotContain("se", Texts(card));
    }

    [Fact]
    public void A_key_field_shows_its_ID_when_the_entry_has_no_name()
    {
        Assert.Equal("she", Field(Open("ana"), "Pronouns").Values.Single().Text);
    }

    [Fact]
    public void A_key_the_data_type_doesnt_list_shows_as_written()
    {
        var services = new ContactServices();
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("bob", "Bob Stan", ("careerPathKey", "zz")));
        services.Disk.SeedDataTypes(services.Clone);
        services.Load();

        var card = ContactCardContent.Of(services.Contacts, ContactAddress.Of("bob"), false)!;

        Assert.Equal("zz", Field(card, "Career path").Values.Single().Text);
    }

    [Fact]
    public void Reports_to_shows_the_managers_full_name()
    {
        Assert.Equal("Ana Pop", Field(Open("bob"), "Reports to").Values.Single().Text);
    }

    [Fact]
    public void Reports_to_shows_the_name_as_written_when_it_finds_nobody()
    {
        _services.Contacts.Save("friends", _services.Member("friends", "dan") with { ReportsTo = "ghost" });

        Assert.Equal("ghost", Field(Open("dan"), "Reports to").Values.Single().Text);
    }

    [Fact]
    public void Direct_reports_list_the_full_names_of_those_who_report_to_the_contact()
    {
        _services.Contacts.Save("friends", _services.Member("friends", "dan") with { ReportsTo = "anap" });

        var card = Open("ana", ctrl: true);

        Assert.Equal(["Bob Stan", "Dan Rus"], Field(card, "Direct reports").Values.Select(value => value.Text));
    }

    [Fact]
    public void A_date_shows_in_the_users_short_date_format()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Equal("3/14/2024", Field(Open("ana", ctrl: true), "Level start date").Values.Single().Text);

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("14.03.2024", Field(Open("ana", ctrl: true), "Level start date").Values.Single().Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void Each_field_reads_as_the_details_pane_reads_it()
    {
        GiveAnaEveryField();
        var model = _services.Model();
        model.Select("team", "ana");

        var card = Open("ana", ctrl: true);

        Assert.Equal(
            model.Details!.Fields.Where(field => field.Label != "Full name").Select(field => (field.Label, string.Join("|", field.Values.Select(value => value.Text)))),
            card.Fields.Select(field => (field.Label, string.Join("|", field.Values.Select(value => value.Text)))));
    }

    // Which contact

    [Fact]
    public void Finds_the_contact_by_the_ID_the_address_names_ignoring_case()
    {
        Assert.Equal("Bob Stan", ContactCardContent.Of(_services.Contacts, ContactAddress.Of("BOB"), false)?.Heading);
    }

    [Fact]
    public void Has_no_card_for_an_ID_no_contact_holds()
    {
        Assert.Null(Card("ghost"));
    }

    [Fact]
    public void Has_no_card_for_an_alias()
    {
        Assert.Null(Card("anap"));
    }

    [Fact]
    public void Has_no_card_for_an_ID_two_contacts_hold()
    {
        var services = ContactServices.WithASharedID();

        Assert.Null(ContactCardContent.Of(services.Contacts, ContactAddress.Of("sam"), false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("task:ana")]
    [InlineData("https://example.com/ana")]
    public void Has_no_card_for_an_address_that_is_not_a_contact_address(string address)
    {
        Assert.Null(ContactCardContent.Of(_services.Contacts, address, false));
    }

    [Fact]
    public void Has_no_card_without_a_clone()
    {
        var services = new ContactServices();

        Assert.Null(ContactCardContent.Of(services.Contacts, ContactAddress.Of("ana"), false));
    }

    [Fact]
    public void Rejects_a_null_address()
    {
        Assert.Throws<ArgumentNullException>(() => ContactCardContent.Of(_services.Contacts, null!, false));
    }

    [Fact]
    public void Rejects_null_contacts()
    {
        Assert.Throws<ArgumentNullException>(() => ContactCardContent.Of(null!, ContactAddress.Of("ana"), false));
    }

    // Ctrl pressed and released while the card shows

    [Fact]
    public void Ctrl_held_at_open_shows_every_field_at_once()
    {
        var card = Open("ana", ctrl: true);

        Assert.Contains("ID", Labels(card));
        Assert.Contains("Direct reports", Labels(card));
    }

    [Fact]
    public void Pressing_Ctrl_switches_the_card_to_every_field()
    {
        var card = Open("ana");

        card.AllDetails = true;

        Assert.Contains("ID", Labels(card));
    }

    [Fact]
    public void Releasing_Ctrl_switches_the_card_back_to_the_common_fields()
    {
        var card = Open("ana", ctrl: true);

        card.AllDetails = false;

        Assert.Equal(CommonOfAna, Labels(card));
    }

    [Fact]
    public void Pressing_and_releasing_Ctrl_again_and_again_gives_the_same_fields_each_time()
    {
        var card = Open("ana");
        var common = Labels(card);
        card.AllDetails = true;
        var all = Labels(card);

        card.AllDetails = false;
        Assert.Equal(common, Labels(card));
        card.AllDetails = true;
        Assert.Equal(all, Labels(card));
    }

    [Fact]
    public void The_heading_stays_while_Ctrl_switches_the_fields()
    {
        var card = Open("ana");

        card.AllDetails = true;

        Assert.Equal(("Ana Pop", "Team"), (card.Heading, card.GroupHeading));
    }

    // The card keeps the contact as it was when it opened

    [Fact]
    public void A_change_to_the_contact_after_the_card_opened_doesnt_reach_the_card()
    {
        var card = Open("ana");

        _services.Contacts.Save("team", _services.Member("team", "ana") with { Title = "Architect", FullName = "Ana Popescu" });

        Assert.Equal("Engineer", Field(card, "Title").Values.Single().Text);
        Assert.Equal("Ana Pop", card.Heading);
    }

    [Fact]
    public void A_change_made_while_the_card_shows_doesnt_reach_the_fields_Ctrl_adds()
    {
        var card = Open("ana");

        _services.Contacts.Save("team", _services.Member("team", "ana") with { Location = "Iasi" });
        card.AllDetails = true;

        Assert.Equal("Cluj", Field(card, "Location").Values.Single().Text);
    }

    [Fact]
    public void A_deleted_manager_after_the_card_opened_still_shows_by_name_on_that_card()
    {
        var card = Open("bob");

        _services.Contacts.Delete("team", "ana");

        Assert.Equal("Ana Pop", Field(card, "Reports to").Values.Single().Text);
    }

    [Fact]
    public void The_next_card_shows_the_contact_as_it_is_then()
    {
        _ = Open("ana");

        _services.Contacts.Save("team", _services.Member("team", "ana") with { Title = "Architect" });

        Assert.Equal("Architect", Field(Open("ana"), "Title").Values.Single().Text);
    }

    [Fact]
    public void A_contact_deleted_before_the_next_card_has_no_card()
    {
        _services.Contacts.Delete("team", "bob");

        Assert.Null(Card("bob"));
    }
}
