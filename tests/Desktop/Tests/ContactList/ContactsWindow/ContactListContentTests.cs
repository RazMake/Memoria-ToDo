using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactsWindow;

/// <summary>What the left side lists, in what order, and what it says when it lists nothing. Selection and its keeping are here too.</summary>
[Unit]
public class ContactListContentTests
{
    private const string NoFiles = "There are no contact files in this repository.";
    private const string Pick = "Pick a contact to see the details.";

    private readonly ContactServices _services = ContactServices.Typical();

    [Fact]
    public void Lists_every_group_in_the_order_of_their_positions_under_Contacts_headings()
    {
        var model = _services.Model();

        Assert.Equal(["Me", "Team", "Ex-Team", "Friends", "Empty"], model.Groups.Select(group => group.Heading));
    }

    [Fact]
    public void Shows_the_users_own_group_like_the_others()
    {
        var model = _services.Model();

        var own = model.Groups.Single(group => group.Heading == "Me");

        Assert.Equal(["Razvan Popov"], own.Contacts.Select(row => row.Contact.FullName));
    }

    [Fact]
    public void Lists_a_groups_contacts_by_full_name()
    {
        var model = _services.Model();

        Assert.Equal(["Ana Pop", "Bob Stan"], model.Groups.Single(group => group.Name == "team").Contacts.Select(row => row.Contact.FullName));
    }

    [Fact]
    public void An_empty_group_still_shows_its_heading()
    {
        var model = _services.Model();

        Assert.Empty(model.Groups.Single(group => group.Heading == "Empty").Contacts);
    }

    [Fact]
    public void Has_no_list_text_while_groups_show()
    {
        Assert.Null(_services.Model().ListText);
    }

    [Fact]
    public void Nothing_is_selected_at_first_and_the_right_side_says_to_pick_a_contact()
    {
        var model = _services.Model();

        Assert.Null(model.Selected);
        Assert.Null(model.Details);
        Assert.Equal(Pick, model.DetailsText);
    }

    [Fact]
    public void With_no_contact_files_the_list_says_so_and_everything_is_off()
    {
        var services = new ContactServices();
        services.Load();

        var model = services.Model();

        Assert.Empty(model.Groups);
        Assert.Equal(NoFiles, model.ListText);
        Assert.True(model.IsOff);
    }

    [Fact]
    public void With_no_clone_selected_the_list_says_there_are_no_contact_files()
    {
        var model = new ContactServices().Model();

        Assert.Equal(NoFiles, model.ListText);
        Assert.True(model.IsOff);
    }

    [Fact]
    public void With_contact_files_the_list_is_not_off()
    {
        Assert.False(_services.Model().IsOff);
    }

    [Fact]
    public void A_clone_whose_group_files_are_all_skipped_is_not_the_no_files_case()
    {
        var services = new ContactServices();
        services.Disk.Seed(services.Clone, "autocomplete/contacts/team.json", "not json");
        services.Load();

        var model = services.Model();

        Assert.Empty(model.Groups);
        Assert.Equal("No contact file could be read. See the problems line.", model.ListText);
        Assert.False(model.IsOff);
        Assert.NotNull(model.Problems.Summary);
    }

    [Fact]
    public void Selecting_a_contact_selects_it_and_shows_its_details_in_place_of_the_prompt()
    {
        var model = _services.Model();

        var selected = model.Select("team", "bob");

        Assert.True(selected);
        Assert.Equal("bob", model.Selected!.Contact.Id);
        Assert.Equal("team", model.Selected.GroupName);
        Assert.Equal("Bob Stan", model.Details!.Heading);
        Assert.Null(model.DetailsText);
    }

    [Fact]
    public void Selecting_another_contact_moves_the_selection()
    {
        var model = _services.Model();
        model.Select("team", "bob");

        model.Select("friends", "dan");

        Assert.Equal("dan", model.Selected!.Contact.Id);
    }

    [Fact]
    public void Shows_new_contacts_at_once_when_Contacts_changes_and_keeps_the_selection_of_one_still_there()
    {
        var model = _services.Model();
        model.Select("team", "bob");

        _services.Contacts.Save("friends", new Shared.Contacts.Contact("eva", "Eva Marin"));
        model.Refresh();

        Assert.Equal(["Dan Rus", "Eva Marin"], model.Groups.Single(group => group.Name == "friends").Contacts.Select(row => row.Contact.FullName));
        Assert.Equal("bob", model.Selected!.Contact.Id);
    }

    [Fact]
    public void Shows_what_changed_in_the_selected_contact_after_Contacts_changes()
    {
        var model = _services.Model();
        model.Select("team", "bob");

        _services.Contacts.Save("team", _services.Member("team", "bob") with { Title = "Lead" });
        model.Refresh();

        Assert.Equal("Lead", model.Selected!.Contact.Title);
    }

    [Fact]
    public void Clears_the_selection_when_the_contact_is_gone_after_Contacts_changes()
    {
        var model = _services.Model();
        model.Select("team", "bob");

        _services.Contacts.Delete("team", "bob");
        model.Refresh();

        Assert.Null(model.Selected);
        Assert.Equal(Pick, model.DetailsText);
    }

    [Fact]
    public void Shows_a_group_whose_file_changed_position_in_its_new_place()
    {
        var model = _services.Model();

        _services.Contacts.MoveGroup("friends", null, "me");
        model.Refresh();

        Assert.Equal(["Friends", "Me", "Team", "Ex-Team", "Empty"], model.Groups.Select(group => group.Heading));
    }
}
