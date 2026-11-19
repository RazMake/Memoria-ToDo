using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>Deleting a contact: when it is offered, the question first, and what stays selected after.</summary>
[Unit]
public class ContactDeleteTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private ContactsModel Selected(string group = "team", string id = "bob")
    {
        var model = _services.Model();
        model.Select(group, id);
        return model;
    }

    [Fact]
    public void Delete_is_offered_for_a_selected_contact_of_another_group()
    {
        Assert.True(Selected().CanDelete);
    }

    [Fact]
    public void Delete_is_not_offered_with_nothing_selected()
    {
        Assert.False(_services.Model().CanDelete);
    }

    [Fact]
    public void Delete_is_not_offered_for_the_users_own_contact()
    {
        Assert.False(Selected("me", "razvanp").CanDelete);
    }

    [Fact]
    public void Delete_is_not_offered_while_a_form_is_open()
    {
        var model = Selected();
        model.BeginEdit();

        Assert.False(model.CanDelete);
    }

    [Fact]
    public void Delete_asks_with_the_full_name_and_removes_the_contact_when_confirmed()
    {
        var model = Selected();
        _services.Prompts.Delete = true;

        var deleted = model.Delete();

        Assert.True(deleted);
        Assert.Equal(["Bob Stan"], _services.Prompts.DeleteAsked);
        Assert.DoesNotContain(_services.Group("team").Members, member => member.Id == "bob");
    }

    [Fact]
    public void Cancel_in_the_question_keeps_the_contact_and_the_selection()
    {
        var model = Selected();
        _services.Prompts.Delete = false;

        var deleted = model.Delete();

        Assert.False(deleted);
        Assert.Contains(_services.Group("team").Members, member => member.Id == "bob");
        Assert.Equal("bob", model.Selected!.Contact.Id);
    }

    [Fact]
    public void After_a_delete_nothing_is_selected_and_the_right_side_says_to_pick_a_contact()
    {
        var model = Selected();
        _services.Prompts.Delete = true;

        model.Delete();

        Assert.Null(model.Selected);
        Assert.Equal("Pick a contact to see the details.", model.DetailsText);
        Assert.DoesNotContain(model.Groups.SelectMany(group => group.Contacts), row => row.Contact.Id == "bob");
    }

    [Fact]
    public void Deleting_the_users_own_contact_asks_nothing_and_does_nothing()
    {
        var model = Selected("me", "razvanp");
        _services.Prompts.Delete = true;

        Assert.False(model.Delete());
        Assert.Empty(_services.Prompts.DeleteAsked);
        Assert.NotNull(_services.Contacts.Me);
    }

    [Fact]
    public void Deleting_with_a_form_open_asks_nothing_and_does_nothing()
    {
        var model = Selected();
        model.BeginEdit();
        _services.Prompts.Delete = true;

        Assert.False(model.Delete());
        Assert.Empty(_services.Prompts.DeleteAsked);
        Assert.Contains(_services.Group("team").Members, member => member.Id == "bob");
    }

    [Fact]
    public void Those_who_reported_to_a_deleted_contact_show_its_name_as_written()
    {
        var model = Selected("team", "ana");
        _services.Prompts.Delete = true;

        model.Delete();
        model.Select("team", "bob");

        var reportsTo = model.Details!.Fields.Single(field => field.Label == "Reports to").Values.Single();
        Assert.Equal(("anap", null), (reportsTo.Text, reportsTo.Target));
    }

    [Fact]
    public void A_delete_that_fails_keeps_the_contact_selected_and_says_why()
    {
        var model = Selected();
        _services.Prompts.Delete = true;
        _services.Disk.FailWritesUnder(_services.Clone);

        var deleted = model.Delete();

        Assert.False(deleted);
        Assert.Equal("bob", model.Selected!.Contact.Id);
        Assert.False(string.IsNullOrWhiteSpace(model.Message));
    }

    [Fact]
    public void A_delete_with_no_repository_folder_says_to_choose_one()
    {
        var model = Selected();
        _services.Prompts.Delete = true;
        _services.Contacts.UseRepositoryFolder(null);

        Assert.False(model.Delete());
        Assert.Equal("There is no repository folder yet. Open Configure and choose one.", model.Message);
    }

    [Fact]
    public void A_delete_in_a_group_file_skipped_since_the_window_showed_it_says_to_see_the_problems_line()
    {
        var model = Selected();
        _services.Prompts.Delete = true;
        _services.Disk.Seed(_services.Clone, TestContactStore.TeamFile, "not json");
        _services.Load();

        Assert.False(model.Delete());
        Assert.Equal("This group's file was skipped. See the problems line.", model.Message);
    }

    [Fact]
    public void Delete_is_off_when_there_are_no_contact_files()
    {
        Assert.False(new ContactServices().Model().CanDelete);
    }
}

/// <summary>Dragging a group's heading: where the group lands, what is off, and what a failed move leaves.</summary>
[Unit]
public class ContactGroupMoveTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private static IReadOnlyList<string> Order(ContactsModel model) => [.. model.Groups.Select(group => group.Name)];

    [Fact]
    public void A_group_moves_before_the_group_it_is_dropped_on()
    {
        var model = _services.Model();

        var moved = model.MoveGroup("ex-team", "team");

        Assert.True(moved);
        Assert.Equal(["me", "ex-team", "team", "friends", "empty"], Order(model));
    }

    [Fact]
    public void A_group_dropped_after_the_last_goes_to_the_end()
    {
        var model = _services.Model();

        model.MoveGroup("team", null);

        Assert.Equal(["me", "ex-team", "friends", "empty", "team"], Order(model));
    }

    [Fact]
    public void The_users_own_group_can_be_moved_too()
    {
        var model = _services.Model();

        model.MoveGroup("me", "friends");

        Assert.Equal(["team", "ex-team", "me", "friends", "empty"], Order(model));
    }

    [Fact]
    public void The_new_order_is_what_Contacts_keeps()
    {
        var model = _services.Model();

        model.MoveGroup("friends", "team");

        Assert.Equal(["me", "friends", "team", "ex-team", "empty"], _services.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public void The_drag_is_on_with_contact_files_and_no_search_or_form()
    {
        Assert.True(_services.Model().CanReorder);
    }

    [Fact]
    public void The_drag_is_off_while_a_search_is_typed()
    {
        var model = _services.Model();
        model.SearchText = "a";

        Assert.False(model.CanReorder);
        Assert.False(model.MoveGroup("friends", "team"));
    }

    [Fact]
    public void The_drag_is_off_while_a_form_is_open()
    {
        var model = _services.Model();
        model.Select("team", "bob");
        model.BeginEdit();

        Assert.False(model.CanReorder);
        Assert.False(model.MoveGroup("friends", "team"));
        Assert.Equal(["me", "team", "ex-team", "friends", "empty"], Order(model));
    }

    [Fact]
    public void The_drag_is_off_with_no_contact_files()
    {
        Assert.False(new ContactServices().Model().CanReorder);
    }

    [Fact]
    public void A_move_that_fails_leaves_the_order_and_says_why()
    {
        var model = _services.Model();
        _services.Disk.FailWritesUnder(_services.Clone);

        var moved = model.MoveGroup("friends", "team");

        Assert.False(moved);
        Assert.Equal(["me", "team", "ex-team", "friends", "empty"], Order(model));
        Assert.False(string.IsNullOrWhiteSpace(model.Message));
    }

    [Fact]
    public void A_move_with_no_repository_folder_says_to_choose_one()
    {
        var model = _services.Model();
        _services.Contacts.UseRepositoryFolder(null);

        Assert.False(model.MoveGroup("friends", "team"));
        Assert.Equal("There is no repository folder yet. Open Configure and choose one.", model.Message);
    }

    [Fact]
    public void A_move_of_a_group_file_skipped_since_the_window_showed_it_says_to_see_the_problems_line()
    {
        var model = _services.Model();
        _services.Disk.Seed(_services.Clone, TestContactStore.TeamFile, "not json");
        _services.Load();

        Assert.False(model.MoveGroup("friends", "team"));
        Assert.Equal("This group's file was skipped. See the problems line.", model.Message);
    }
}

/// <summary>The problems line: what Contacts found wrong with its files and names, in the window's words.</summary>
[Unit]
public class ContactProblemsTests
{
    [Fact]
    public void Nothing_wrong_gives_no_summary_and_no_items()
    {
        var problems = ContactServices.Typical().Model().Problems;

        Assert.Null(problems.Summary);
        Assert.Empty(problems.Items);
    }

    [Fact]
    public void Skipped_files_and_shared_names_are_counted_in_one_summary()
    {
        var services = ContactServices.Typical();
        services.Disk.Seed(services.Clone, "autocomplete/contacts/broken-one.json", "not json");
        services.Disk.Seed(services.Clone, "autocomplete/contacts/broken-two.json", "also not json");
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("dup", "Dup One"));
        services.Disk.SeedGroup(services.Clone, "friends", 3000, ContactFiles.Person("dup", "Dup Two"));
        services.Load();

        var problems = services.Model().Problems;

        Assert.Equal("2 contact files skipped, 1 name shared by more than one contact", problems.Summary);
    }

    [Fact]
    public void One_skipped_file_alone_says_so_in_the_singular_and_names_no_shared_names()
    {
        var services = ContactServices.Typical();
        services.Disk.Seed(services.Clone, "autocomplete/contacts/broken.json", "not json");
        services.Load();

        Assert.Equal("1 contact file skipped", services.Model().Problems.Summary);
    }

    [Fact]
    public void Two_shared_names_alone_say_so_in_the_plural_and_name_no_skipped_files()
    {
        var services = ContactServices.Typical();
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("dup", "Dup One"), ContactFiles.Person("dap", "Dap One"));
        services.Disk.SeedGroup(services.Clone, "friends", 3000, ContactFiles.Person("dup", "Dup Two"), ContactFiles.Person("dap", "Dap Two"));
        services.Load();

        Assert.Equal("2 names shared by more than one contact", services.Model().Problems.Summary);
    }

    [Fact]
    public void One_skipped_file_and_one_shared_name_are_both_in_the_singular_joined_by_a_comma()
    {
        var services = ContactServices.Typical();
        services.Disk.Seed(services.Clone, "autocomplete/contacts/broken.json", "not json");
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("dup", "Dup One"));
        services.Disk.SeedGroup(services.Clone, "friends", 3000, ContactFiles.Person("dup", "Dup Two"));
        services.Load();

        Assert.Equal("1 contact file skipped, 1 name shared by more than one contact", services.Model().Problems.Summary);
    }

    [Fact]
    public void Each_shared_name_and_each_skipped_file_has_its_own_line()
    {
        var services = ContactServices.Typical();
        services.Disk.Seed(services.Clone, "autocomplete/contacts/broken.json", "not json");
        services.Disk.SeedGroup(services.Clone, "team", 1000, ContactFiles.Person("dup", "Dup One"));
        services.Disk.SeedGroup(services.Clone, "friends", 3000, ContactFiles.Person("dup", "Dup Two"));
        services.Load();

        var items = services.Model().Problems.Items;

        Assert.Contains("Shared by more than one contact: dup", items);
        Assert.Contains(items, item => item.Contains("autocomplete/contacts/broken.json") || item.Contains(Path.Combine("autocomplete", "contacts", "broken.json")));
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void The_problems_follow_Contacts_after_a_Refresh()
    {
        var services = ContactServices.Typical();
        var model = services.Model();
        Assert.Null(model.Problems.Summary);

        services.Disk.Seed(services.Clone, "autocomplete/contacts/broken.json", "not json");
        services.Load();
        model.Refresh();

        Assert.NotNull(model.Problems.Summary);
    }
}
