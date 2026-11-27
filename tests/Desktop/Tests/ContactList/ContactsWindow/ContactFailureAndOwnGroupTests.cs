using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactsWindow;

/// <summary>A change Contacts saved before a notice failed counts as saved: the window shows the shared message, reloads, and logs the failure once.</summary>
[Unit]
public class ContactNoticeFailureTests
{
    private const string Failure = "A Changed subscriber failed.";
    private const string Message = "Saved, but the app couldn't finish updating: " + Failure;

    private readonly ContactServices _services = ContactServices.Typical();

    private ContactsModel ModelWithFailingNotice()
    {
        var model = new ContactsModel(_services.Contacts, _services.Prompts, _services.Log.CreateLogger());
        _services.Contacts.Changed += (_, _) => throw new InvalidOperationException(Failure);
        return model;
    }

    private void AssertLoggedOnce() =>
        Assert.Single(_services.Log.Events.At(Serilog.Events.LogEventLevel.Error));

    [Fact]
    public void A_save_Contacts_kept_before_a_notice_failed_closes_the_form_shows_the_message_and_reloads_the_groups()
    {
        var model = ModelWithFailingNotice();
        model.Select("team", "bob");
        model.BeginEdit();
        model.Form!["title"] = "Lead";

        var saved = model.SaveForm();

        Assert.True(saved);
        Assert.Null(model.Form);
        Assert.Equal(Message, model.Message);
        Assert.Equal(_services.Model().Groups.Select(group => group.Name), model.Groups.Select(group => group.Name));
        AssertLoggedOnce();
    }

    [Fact]
    public void A_delete_Contacts_kept_before_a_notice_failed_shows_the_message_and_reloads_the_groups()
    {
        var model = ModelWithFailingNotice();
        model.Select("team", "bob");
        _services.Prompts.Delete = true;

        var deleted = model.Delete();

        Assert.True(deleted);
        Assert.Equal(Message, model.Message);
        Assert.DoesNotContain(model.Groups.Single(group => group.Name == "team").Contacts, contact => contact.Contact.Id == "bob");
        AssertLoggedOnce();
    }

    [Fact]
    public void A_group_move_Contacts_kept_before_a_notice_failed_shows_the_message_and_reloads_the_groups()
    {
        var model = ModelWithFailingNotice();

        model.MoveGroup("friends", "team");

        Assert.Equal(Message, model.Message);
        Assert.Equal(_services.Model().Groups.Select(group => group.Name), model.Groups.Select(group => group.Name));
        AssertLoggedOnce();
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
