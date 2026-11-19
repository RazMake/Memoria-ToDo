using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>Failures the window doesn't know: only a skipped group's rejection gets a message, anything else reaches the app's unhandled-error path.</summary>
[Unit]
public class ContactUnexpectedFailureTests
{
    private const string Failure = "A Changed subscriber failed.";

    private readonly ContactServices _services = ContactServices.Typical();

    // Contacts raises Changed after it wrote, on the thread of the call, and lets a subscriber's failure out as it is.
    private void MakeChangedFail() => _services.Contacts.Changed += (_, _) => throw new InvalidOperationException(Failure);

    [Fact]
    public void A_save_that_throws_InvalidOperationException_while_its_group_is_still_loaded_escapes()
    {
        var model = _services.Model();
        model.Select("team", "bob");
        model.BeginEdit();
        model.Form!["title"] = "Lead";
        MakeChangedFail();

        var thrown = Assert.Throws<InvalidOperationException>(() => model.SaveForm());

        Assert.Equal(Failure, thrown.Message);
        Assert.Null(model.Message);
    }

    [Fact]
    public void A_delete_that_throws_InvalidOperationException_while_its_group_is_still_loaded_escapes()
    {
        var model = _services.Model();
        model.Select("team", "bob");
        _services.Prompts.Delete = true;
        MakeChangedFail();

        var thrown = Assert.Throws<InvalidOperationException>(() => model.Delete());

        Assert.Equal(Failure, thrown.Message);
        Assert.Null(model.Message);
    }

    [Fact]
    public void A_move_that_throws_InvalidOperationException_while_its_groups_are_still_loaded_escapes()
    {
        var model = _services.Model();
        MakeChangedFail();

        var thrown = Assert.Throws<InvalidOperationException>(() => model.MoveGroup("friends", "team"));

        Assert.Equal(Failure, thrown.Message);
        Assert.Null(model.Message);
    }
}

/// <summary>The user's own group is found in a file named "Me.json" as in "me.json": it takes no new contact, and its contact has no Delete.</summary>
[Unit]
public class ContactOwnGroupNameTests
{
    private readonly ContactServices _services = ContactServices.OwnFileNamed("Me.json");

    [Fact]
    public void Add_is_not_offered_on_the_users_own_group_whatever_its_files_case()
    {
        var model = _services.Model();

        Assert.Equal(["Me", "friends"], model.Groups.Select(group => group.Name));
        Assert.Equal(["friends"], model.Groups.Where(group => group.CanAdd).Select(group => group.Name));
    }

    [Fact]
    public void Add_on_the_users_own_group_does_nothing_whatever_its_files_case()
    {
        var model = _services.Model();

        Assert.False(model.BeginAdd("Me"));
        Assert.Null(model.Form);
    }

    [Fact]
    public void Delete_is_not_offered_for_the_users_own_contact_whatever_its_files_case()
    {
        var model = _services.Model();
        model.Select("Me", "razvanp");
        _services.Prompts.Delete = true;

        Assert.False(model.CanDelete);
        Assert.False(model.Delete());
        Assert.Empty(_services.Prompts.DeleteAsked);
        Assert.NotNull(_services.Contacts.Me);
    }

    [Fact]
    public void Edit_is_still_offered_on_the_users_own_contact_whatever_its_files_case()
    {
        var model = _services.Model();
        model.Select("Me", "razvanp");

        Assert.True(model.CanEdit);
    }
}
