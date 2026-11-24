using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactsWindow;

/// <summary>What the search text leaves of the list, through Contacts' search, and what the list says when nothing matches.</summary>
[Unit]
public class ContactListSearchTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private static IReadOnlyList<string> Names(ContactsModel model) =>
        [.. model.Groups.SelectMany(group => group.Contacts).Select(row => row.Contact.FullName)];

    [Fact]
    public void Shows_only_the_contacts_that_match_every_typed_word_ignoring_case()
    {
        var model = _services.Model();

        model.SearchText = "POP ana";

        Assert.Equal(["Ana Pop"], Names(model));
    }

    [Fact]
    public void Matches_any_field_not_only_the_name()
    {
        var model = _services.Model();

        model.SearchText = "oradea";

        Assert.Equal(["Dan Rus"], Names(model));
    }

    [Fact]
    public void Hides_a_group_with_no_match_empty_groups_included()
    {
        var model = _services.Model();

        model.SearchText = "ana";

        Assert.Equal(["Team"], model.Groups.Select(group => group.Heading));
    }

    [Fact]
    public void Keeps_matches_in_their_groups_and_the_groups_in_their_order()
    {
        var model = _services.Model();

        model.SearchText = "a";

        Assert.Equal(["Me", "Team", "Friends"], model.Groups.Select(group => group.Heading));
        Assert.Equal(["Ana Pop", "Bob Stan"], model.Groups.Single(group => group.Name == "team").Contacts.Select(row => row.Contact.FullName));
    }

    [Fact]
    public void Shows_each_match_once()
    {
        var model = _services.Model();

        model.SearchText = "a";

        Assert.Equal(["Razvan Popov", "Ana Pop", "Bob Stan", "Dan Rus"], Names(model));
    }

    [Fact]
    public void With_no_match_at_all_the_list_says_no_contact_matches()
    {
        var model = _services.Model();

        model.SearchText = "zzz";

        Assert.Empty(model.Groups);
        Assert.Equal("No contact matches the search.", model.ListText);
    }

    [Fact]
    public void Emptying_the_search_shows_every_group_again_empty_ones_included()
    {
        var model = _services.Model();
        model.SearchText = "zzz";

        model.SearchText = "";

        Assert.Equal(["Me", "Team", "Ex-Team", "Friends", "Empty"], model.Groups.Select(group => group.Heading));
        Assert.Null(model.ListText);
    }

    [Fact]
    public void Keeps_the_selection_when_the_selected_contact_still_matches()
    {
        var model = _services.Model();
        model.Select("team", "bob");

        model.SearchText = "stan";

        Assert.Equal("bob", model.Selected!.Contact.Id);
    }

    [Fact]
    public void Clears_the_selection_when_the_selected_contact_no_longer_matches()
    {
        var model = _services.Model();
        model.Select("team", "bob");

        model.SearchText = "oradea";

        Assert.Null(model.Selected);
        Assert.Equal("Pick a contact to see the details.", model.DetailsText);
    }

    [Fact]
    public void Selecting_a_contact_the_search_hides_empties_the_search_first_and_selects_it()
    {
        var model = _services.Model();
        model.Select("team", "bob");
        model.SearchText = "stan";

        var selected = model.Select("team", "ana");

        Assert.True(selected);
        Assert.Equal("", model.SearchText);
        Assert.Equal("ana", model.Selected!.Contact.Id);
        Assert.Equal(["Me", "Team", "Ex-Team", "Friends", "Empty"], model.Groups.Select(group => group.Heading));
    }

    [Fact]
    public void Selecting_a_contact_the_search_still_shows_keeps_the_search()
    {
        var model = _services.Model();
        model.SearchText = "a";

        model.Select("team", "ana");

        Assert.Equal("a", model.SearchText);
        Assert.Equal("ana", model.Selected!.Contact.Id);
    }

    [Fact]
    public void A_click_on_a_direct_report_the_search_hides_empties_the_search_and_selects_it()
    {
        var model = _services.Model();
        model.Select("team", "ana");
        model.SearchText = "pop";
        var report = model.Details!.Fields.Single(field => field.Label == "Direct reports").Values.Single().Target!;

        model.Select(report.GroupName, report.Id);

        Assert.Equal("", model.SearchText);
        Assert.Equal("bob", model.Selected!.Contact.Id);
    }

    [Theory]
    [InlineData("alpha", "ex-team", "Sam Alpha")]
    [InlineData("beta", "friends", "Sam Beta")]
    public void A_search_matching_only_one_of_two_contacts_that_share_an_ID_shows_only_that_one_in_its_own_group(string search, string group, string fullName)
    {
        var services = ContactServices.WithASharedID();
        Assert.Equal(["sam"], services.Contacts.SharedNames);
        var model = services.Model();

        model.SearchText = search;

        Assert.Equal([group], model.Groups.Select(shown => shown.Name));
        Assert.Equal([fullName], Names(model));
    }

    [Fact]
    public void A_search_matching_both_contacts_that_share_an_ID_shows_each_in_its_own_group()
    {
        var model = ContactServices.WithASharedID().Model();

        model.SearchText = "sam";

        Assert.Equal(["ex-team", "friends"], model.Groups.Select(shown => shown.Name));
        Assert.Equal(["Sam Alpha", "Sam Beta"], Names(model));
    }

    [Fact]
    public void A_search_follows_a_change_to_Contacts_after_Refresh()
    {
        var model = _services.Model();
        model.SearchText = "eva";

        _services.Contacts.Save("friends", new Contact("eva", "Eva Marin"));
        model.Refresh();

        Assert.Equal(["Eva Marin"], Names(model));
    }
}
