using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>Editing and adding through the model: opening the form, saving through Contacts, and what Contacts' rejections and a sync do to it.</summary>
[Unit]
public class ContactEditingTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private ContactsModel Selected(string group = "team", string id = "bob")
    {
        var model = _services.Model();
        model.Select(group, id);
        return model;
    }

    [Fact]
    public void Edit_opens_a_form_on_the_selected_contact()
    {
        var model = Selected();

        var opened = model.BeginEdit();

        Assert.True(opened);
        Assert.Equal("Bob Stan", model.Form!["fullName"]);
        Assert.False(model.Form.IsNew);
    }

    [Fact]
    public void Edit_with_nothing_selected_does_nothing()
    {
        var model = _services.Model();

        Assert.False(model.BeginEdit());
        Assert.Null(model.Form);
    }

    [Fact]
    public void Edit_is_offered_only_with_a_selected_contact_and_contact_files()
    {
        var model = _services.Model();
        Assert.False(model.CanEdit);

        model.Select("team", "bob");
        Assert.True(model.CanEdit);

        Assert.False(new ContactServices().Model().CanEdit);
    }

    [Fact]
    public void Edit_is_offered_on_the_users_own_contact_too()
    {
        Assert.True(Selected("me", "razvanp").CanEdit);
    }

    [Fact]
    public void A_form_replaces_the_details_while_it_is_open()
    {
        var model = Selected();

        model.BeginEdit();

        Assert.NotNull(model.Form);
        Assert.Null(model.DetailsText);
    }

    [Fact]
    public void Saving_writes_the_change_through_Contacts_and_leaves_the_form()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["title"] = "Lead";

        var saved = model.SaveForm();

        Assert.True(saved);
        Assert.Null(model.Form);
        Assert.Equal("Lead", _services.Member("team", "bob").Title);
        Assert.Null(model.Message);
    }

    [Fact]
    public void Saving_selects_the_saved_contact_and_shows_its_new_details()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["title"] = "Lead";

        model.SaveForm();

        Assert.Equal("bob", model.Selected!.Contact.Id);
        Assert.Equal("Lead", model.Selected.Title);
        Assert.NotNull(model.Details);
    }

    [Fact]
    public void Saving_a_form_with_problems_writes_nothing_and_keeps_the_form()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["dayStartTime"] = "25:00";
        var before = _services.Bytes(TestContactStore.TeamFile);

        var saved = model.SaveForm();

        Assert.False(saved);
        Assert.NotNull(model.Form);
        Assert.Equal(before, _services.Bytes(TestContactStore.TeamFile));
    }

    [Fact]
    public void A_save_Contacts_rejects_keeps_the_form_with_its_typed_values_and_shows_the_reason_at_the_top()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["alias"] = "anap";
        var reason = Assert.Throws<ArgumentException>(() => _services.Contacts.Save("team", model.Form.ToContact())).Message;

        var saved = model.SaveForm();

        Assert.False(saved);
        Assert.NotNull(model.Form);
        Assert.Equal("anap", model.Form["alias"]);
        Assert.Equal(reason, model.Message);
        Assert.Equal("", _services.Member("team", "bob").Alias ?? "");
    }

    [Fact]
    public void A_save_that_fails_to_write_keeps_the_form_and_says_why()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["title"] = "Lead";
        _services.Disk.FailWritesUnder(_services.Clone);

        var saved = model.SaveForm();

        Assert.False(saved);
        Assert.Equal("Lead", model.Form["title"]);
        Assert.False(string.IsNullOrWhiteSpace(model.Message));
    }

    [Fact]
    public void A_save_with_no_repository_folder_says_to_choose_one()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["title"] = "Lead";
        _services.Contacts.UseRepositoryFolder(null);

        var saved = model.SaveForm();

        Assert.False(saved);
        Assert.Equal("There is no repository folder yet. Open Configure and choose one.", model.Message);
        Assert.Equal("Lead", model.Form["title"]);
    }

    [Fact]
    public void A_save_into_a_group_file_skipped_since_the_window_showed_it_says_to_see_the_problems_line()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["title"] = "Lead";
        _services.Disk.Seed(_services.Clone, TestContactStore.TeamFile, "not json");
        _services.Load();

        var saved = model.SaveForm();

        Assert.False(saved);
        Assert.Equal("This group's file was skipped. See the problems line.", model.Message);
        Assert.NotNull(model.Form);
    }

    [Fact]
    public void The_message_clears_when_the_next_save_works()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["dayStartTime"] = "";
        model.Form["alias"] = "anap";
        model.SaveForm();
        Assert.NotNull(model.Message);

        model.Form["alias"] = "bobs";
        model.SaveForm();

        Assert.Null(model.Message);
        Assert.Null(model.Form);
    }

    [Fact]
    public void Add_opens_an_empty_form_for_the_group()
    {
        var model = _services.Model();

        var opened = model.BeginAdd("friends");

        Assert.True(opened);
        Assert.True(model.Form!.IsNew);
        Assert.Equal("friends", model.Form.GroupName);
    }

    [Fact]
    public void Saving_a_new_contact_adds_it_to_the_group_selects_it_and_leaves_the_form()
    {
        var model = _services.Model();
        model.BeginAdd("friends");
        model.Form!["id"] = "eva";
        model.Form["fullName"] = "Eva Marin";

        var saved = model.SaveForm();

        Assert.True(saved);
        Assert.Null(model.Form);
        Assert.Equal(("friends", "eva"), (model.Selected!.GroupName, model.Selected.Contact.Id));
        Assert.Equal("Eva Marin", _services.Member("friends", "eva").FullName);
    }

    [Fact]
    public void Adding_with_an_ID_Contacts_rejects_keeps_the_form_and_shows_Contacts_reason()
    {
        var model = _services.Model();
        model.BeginAdd("friends");
        model.Form!["id"] = "not an id";
        model.Form["fullName"] = "Eva Marin";
        var reason = Assert.Throws<ArgumentException>(() => _services.Contacts.Save("friends", model.Form.ToContact())).Message;

        var saved = model.SaveForm();

        Assert.False(saved);
        Assert.NotNull(model.Form);
        Assert.Equal(reason, model.Message);
    }

    [Theory]
    [InlineData("friends", "dan")]
    [InlineData("friends", "DAN")]
    [InlineData("friends", "ana")]
    [InlineData("friends", "RazvanP")]
    [InlineData("team", "dan")]
    public void Adding_with_an_ID_a_loaded_contact_holds_writes_nothing_and_keeps_the_form_with_the_problem_under_the_ID(string group, string id)
    {
        var model = _services.Model();
        var files = new[] { TestContactStore.MeFile, TestContactStore.TeamFile, TestContactStore.Folder + "/friends.json" };
        var before = files.Select(_services.Bytes).ToList();
        var changes = 0;
        _services.Contacts.Changed += (_, _) => changes++;
        model.BeginAdd(group);
        model.Form!["id"] = id;
        model.Form["fullName"] = "Another One";

        var saved = model.SaveForm();

        Assert.False(saved);
        Assert.NotNull(model.Form);
        Assert.Equal("Another contact has this ID.", model.Form.ProblemOf("id"));
        Assert.Null(model.Message);
        Assert.Equal(0, changes);
        Assert.Equal(before, files.Select(_services.Bytes));
    }

    [Fact]
    public void A_form_that_edits_a_contact_saves_it_under_its_own_ID()
    {
        var model = Selected("friends", "dan");
        model.BeginEdit();
        model.Form!["title"] = "Friend";

        Assert.True(model.SaveForm());
        Assert.Equal("Friend", _services.Member("friends", "dan").Title);
    }

    [Fact]
    public void A_data_type_that_stops_loading_while_a_form_is_open_leaves_the_form_open_with_its_values_and_a_save_keeps_the_key()
    {
        var model = Selected("team", "ana");
        model.BeginEdit();
        var form = model.Form!;
        form["title"] = "Lead";
        _services.Disk.Seed(_services.Clone, TestContactStore.CareerPathsFile, "not json");
        _services.Load();

        model.Refresh();

        Assert.Same(form, model.Form);
        Assert.Equal("Lead", form["title"]);
        Assert.Equal("se", form["careerPathKey"]);
        Assert.True(form.IsReadOnly("careerPathKey"));
        Assert.True(model.SaveForm());
        Assert.Equal(("Lead", "se"), (_services.Member("team", "ana").Title, _services.Member("team", "ana").CareerPathKey));
    }

    [Fact]
    public void Add_is_offered_on_every_group_but_the_users_own()
    {
        var model = _services.Model();

        Assert.Equal(["team", "ex-team", "friends", "empty"], model.Groups.Where(group => group.CanAdd).Select(group => group.Name));
    }

    [Fact]
    public void Add_on_the_users_own_group_does_nothing()
    {
        var model = _services.Model();

        Assert.False(model.BeginAdd("me"));
        Assert.Null(model.Form);
    }

    [Fact]
    public void Add_is_off_when_there_are_no_contact_files()
    {
        Assert.False(new ContactServices().Model().BeginAdd("friends"));
    }

    [Fact]
    public void Add_works_on_a_group_with_no_contacts()
    {
        var model = _services.Model();

        Assert.True(model.BeginAdd("empty"));
    }

    [Fact]
    public void Add_replaces_the_details_of_the_selected_contact_with_the_new_form()
    {
        var model = Selected();

        model.BeginAdd("friends");

        Assert.True(model.Form!.IsNew);
        Assert.Null(model.DetailsText);
    }

    [Fact]
    public void A_sync_that_changes_the_contact_being_edited_keeps_the_typed_values_and_the_save_writes_them_over()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["title"] = "Lead";
        _services.Contacts.Save("team", _services.Member("team", "bob") with { Title = "Synced", Location = "Iasi" });
        model.Refresh();

        Assert.Equal("Lead", model.Form["title"]);
        model.SaveForm();

        Assert.Equal("Lead", _services.Member("team", "bob").Title);
    }

    [Fact]
    public void A_sync_that_deleted_the_contact_being_edited_leaves_the_form_open_and_the_save_adds_it_back()
    {
        var model = Selected();
        model.BeginEdit();
        model.Form!["title"] = "Lead";
        _services.Contacts.Delete("team", "bob");
        model.Refresh();

        Assert.NotNull(model.Form);
        var saved = model.SaveForm();

        Assert.True(saved);
        Assert.Equal("Lead", _services.Member("team", "bob").Title);
    }
}

/// <summary>Leaving a form that has changes: when the model asks Save, Discard, or Cancel, and what each answer does.</summary>
[Unit]
public class ContactLeavingFormTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private ContactsModel Editing(bool change)
    {
        var model = _services.Model();
        model.Select("team", "bob");
        model.BeginEdit();
        if (change)
            model.Form!["title"] = "Lead";
        return model;
    }

    [Fact]
    public void Selecting_another_contact_with_no_changes_leaves_the_form_without_asking()
    {
        var model = Editing(change: false);

        var moved = model.Select("friends", "dan");

        Assert.True(moved);
        Assert.Null(model.Form);
        Assert.Equal("dan", model.Selected!.Contact.Id);
        Assert.Equal(0, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void Esc_with_no_changes_leaves_the_form_without_asking()
    {
        var model = Editing(change: false);

        Assert.True(model.LeaveForm());
        Assert.Null(model.Form);
        Assert.Equal(0, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void Esc_with_no_form_open_has_nothing_to_leave_and_asks_nothing()
    {
        var model = _services.Model();

        Assert.True(model.LeaveForm());
        Assert.Equal(0, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void Selecting_another_contact_with_changes_asks_and_Cancel_keeps_the_form_and_the_selection()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Cancel;

        var moved = model.Select("friends", "dan");

        Assert.False(moved);
        Assert.Equal("Lead", model.Form!["title"]);
        Assert.Equal("bob", model.Selected!.Contact.Id);
        Assert.Equal(1, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void Selecting_another_contact_with_changes_and_Discard_drops_the_changes_and_moves()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Discard;

        var moved = model.Select("friends", "dan");

        Assert.True(moved);
        Assert.Null(model.Form);
        Assert.Equal("dan", model.Selected!.Contact.Id);
        Assert.Equal("Designer", _services.Member("team", "bob").Title);
    }

    [Fact]
    public void Selecting_another_contact_with_changes_and_Save_saves_then_moves()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Save;

        var moved = model.Select("friends", "dan");

        Assert.True(moved);
        Assert.Equal("Lead", _services.Member("team", "bob").Title);
        Assert.Equal("dan", model.Selected!.Contact.Id);
    }

    [Fact]
    public void Save_in_the_question_that_fails_keeps_the_form_and_the_selection_and_says_why()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Save;
        _services.Disk.FailWritesUnder(_services.Clone);

        var moved = model.Select("friends", "dan");

        Assert.False(moved);
        Assert.NotNull(model.Form);
        Assert.Equal("bob", model.Selected!.Contact.Id);
        Assert.False(string.IsNullOrWhiteSpace(model.Message));
    }

    [Fact]
    public void Add_with_changes_asks_first_and_Cancel_keeps_the_form()
    {
        var model = Editing(change: true);

        var opened = model.BeginAdd("friends");

        Assert.False(opened);
        Assert.False(model.Form!.IsNew);
        Assert.Equal(1, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void Add_with_changes_and_Discard_opens_the_new_form()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Discard;

        Assert.True(model.BeginAdd("friends"));
        Assert.True(model.Form!.IsNew);
    }

    [Fact]
    public void Edit_while_a_form_is_open_does_nothing_and_asks_nothing()
    {
        var model = Editing(change: true);
        var form = model.Form;

        var opened = model.BeginEdit();

        Assert.False(opened);
        Assert.Same(form, model.Form);
        Assert.Equal("Lead", model.Form!["title"]);
        Assert.Equal(0, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void Edit_while_a_form_with_no_changes_is_open_does_nothing_and_keeps_the_same_form()
    {
        var model = Editing(change: false);
        var form = model.Form;

        Assert.False(model.BeginEdit());

        Assert.Same(form, model.Form);
        Assert.Equal(0, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void Esc_with_changes_and_Cancel_keeps_the_form()
    {
        var model = Editing(change: true);

        Assert.False(model.LeaveForm());
        Assert.NotNull(model.Form);
    }

    [Fact]
    public void Esc_with_changes_and_Discard_leaves_the_form_and_keeps_the_contact_selected()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Discard;

        Assert.True(model.LeaveForm());
        Assert.Null(model.Form);
        Assert.Equal("bob", model.Selected!.Contact.Id);
    }

    [Fact]
    public void Esc_with_changes_and_Save_saves_and_leaves_the_form()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Save;

        Assert.True(model.LeaveForm());
        Assert.Null(model.Form);
        Assert.Equal("Lead", _services.Member("team", "bob").Title);
    }

    [Fact]
    public void The_window_may_close_with_no_form_without_asking()
    {
        Assert.True(_services.Model().MayClose());
        Assert.Equal(0, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void The_window_may_close_with_an_unchanged_form_without_asking()
    {
        Assert.True(Editing(change: false).MayClose());
        Assert.Equal(0, _services.Prompts.LeaveAsked);
    }

    [Fact]
    public void Closing_with_changes_and_Cancel_stays_open()
    {
        Assert.False(Editing(change: true).MayClose());
    }

    [Fact]
    public void Closing_with_changes_and_Discard_closes_without_saving()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Discard;

        Assert.True(model.MayClose());
        Assert.Equal("Designer", _services.Member("team", "bob").Title);
    }

    [Fact]
    public void Closing_with_changes_and_Save_saves_then_closes()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Save;

        Assert.True(model.MayClose());
        Assert.Equal("Lead", _services.Member("team", "bob").Title);
    }

    [Fact]
    public void Closing_with_changes_and_a_save_that_fails_stays_open_and_says_why()
    {
        var model = Editing(change: true);
        _services.Prompts.Leave = LeaveAnswer.Save;
        _services.Disk.FailWritesUnder(_services.Clone);

        Assert.False(model.MayClose());
        Assert.NotNull(model.Form);
        Assert.False(string.IsNullOrWhiteSpace(model.Message));
    }

    [Fact]
    public void Closing_while_the_question_already_shows_asks_once_and_the_second_close_is_refused()
    {
        var model = Editing(change: true);
        bool? second = null;
        _services.Prompts.Leave = LeaveAnswer.Discard;
        _services.Prompts.WhileAsking = () => second = model.MayClose();

        var first = model.MayClose();

        Assert.False(second);
        Assert.True(first);
        Assert.Equal(1, _services.Prompts.LeaveAsked);
    }
}
