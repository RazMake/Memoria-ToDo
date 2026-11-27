// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using Grpc.Core;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>What the Contacts calls that change contacts do and refuse: save, delete, and move a group.</summary>
[Unit]
public class LocalApiContactChangeTests
{
    private const string BrokenFile = TestContactStore.Folder + "/broken.json";

    private static V1.SaveContactRequest Save(string group, V1.Contact contact) => new() { Group = group, Contact = contact };

    [Fact]
    public async Task SaveContact_replaces_the_whole_contact_and_returns_it()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var saved = await api.ContactCalls.SaveContactAsync(Save("team", ContactSeeds.Contact(("id", "1"), ("fullName", "Ana P."))));

        Assert.Equal(ContactSeeds.Map(("id", "1"), ("fullName", "Ana P.")), saved.Map());
        var stored = api.ParsedContactFile(TestContactStore.TeamFile);
        Assert.Equal(ContactSeeds.Map(("id", "1"), ("fullName", "Ana P.")), new Dictionary<string, string>(stored.WithId("1")));
        Assert.Equal(["1", "2"], stored.Ids);
    }

    [Fact]
    public async Task SaveContact_adds_a_contact_the_group_does_not_hold()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        await api.ContactCalls.SaveContactAsync(Save("team", ContactSeeds.Contact(("id", "9"), ("fullName", "Eva Ilie"), ("title", "Tester"))));

        var found = await api.ContactCalls.GetContactAsync("9");
        Assert.Equal(ContactSeeds.Map(("id", "9"), ("fullName", "Eva Ilie"), ("title", "Tester")), found.Map());
        Assert.Contains("9", api.ParsedContactFile(TestContactStore.TeamFile).Ids);
    }

    [Fact]
    public async Task A_contact_read_and_then_saved_keeps_every_stored_value_including_an_empty_nickname()
    {
        await using var api = new ApiHarness();
        api.Files.SeedDataTypes(api.Clone);
        api.Files.SeedGroup(api.Clone, "team", 1000, ContactFiles.Person("7", "Dan Pop",
            ("alias", "dan"), ("firstName", "Dan"), ("nickname", ""), ("title", "Lead"), ("location", "Cluj"),
            ("levelId", "L5"), ("workingType", "remote"), ("pronounsKey", "she"), ("careerPathKey", "em"), ("reportsTo", "1"),
            ("dayStartTime", "09:30"), ("levelStartDate", "2024-01-31"), ("teamMemberSince", "2023-05-01"), ("exitDate", "2026-12-31")));
        api.LoadContacts();
        var before = api.Contacts.Find("7");

        var read = await api.ContactCalls.GetContactAsync("7");
        await api.ContactCalls.SaveContactAsync(Save("team", read));

        Assert.Equal(before, api.Contacts.Find("7"));
        Assert.NotEqual("Dan", api.ParsedContactFile(TestContactStore.TeamFile).WithId("7").GetValueOrDefault("nickname"));
    }

    [Fact]
    public async Task SaveContact_into_an_unknown_group_fails_with_INVALID_ARGUMENT()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.SaveContactAsync(Save("nowhere", ContactSeeds.Contact(("id", "9"), ("fullName", "Eva")))));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task SaveContact_without_a_full_name_fails_with_INVALID_ARGUMENT_and_changes_nothing()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        var before = api.Contacts.Find("1");

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.SaveContactAsync(Save("team", ContactSeeds.Contact(("id", "1")))));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Equal(before, api.Contacts.Find("1"));
    }

    [Fact]
    public async Task SaveContact_with_a_time_in_another_form_fails_with_INVALID_ARGUMENT()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.SaveContactAsync(
            Save("team", ContactSeeds.Contact(("id", "1"), ("fullName", "Ana Popa"), ("dayStartTime", "9am")))));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task SaveContact_with_a_field_name_Contacts_does_not_know_fails_with_INVALID_ARGUMENT_instead_of_dropping_it()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        var before = api.Contacts.Find("1");

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.SaveContactAsync(
            Save("team", ContactSeeds.Contact(("id", "1"), ("fullName", "Ana Popa"), ("favoriteColor", "blue")))));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Equal(before, api.Contacts.Find("1"));
    }

    [Fact]
    public async Task SaveContact_without_a_contact_fails_with_INVALID_ARGUMENT()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.SaveContactAsync(new V1.SaveContactRequest { Group = "team" }));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task SaveContact_into_a_group_whose_file_was_skipped_fails_with_FAILED_PRECONDITION()
    {
        await using var api = new ApiHarness();
        api.Files.Seed(api.Clone, BrokenFile, "{ not json");
        api.LoadContacts();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.SaveContactAsync(Save("broken", ContactSeeds.Contact(("id", "1"), ("fullName", "Ana")))));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task SaveContact_of_the_own_contact_when_me_json_was_skipped_fails_with_FAILED_PRECONDITION()
    {
        await using var api = new ApiHarness();
        api.Files.Seed(api.Clone, TestContactStore.MeFile, "{ not json");
        api.LoadContacts();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.SaveContactAsync(Save("me", ContactSeeds.Contact(("id", "42"), ("fullName", "Me")))));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task SaveContact_fails_with_FAILED_PRECONDITION_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.SaveContactAsync(Save("team", ContactSeeds.Contact(("id", "1"), ("fullName", "Ana")))));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task DeleteContact_removes_the_contact_from_its_group()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        await api.ContactCalls.DeleteContactAsync("team", "2");

        Assert.Equal(["1"], api.ParsedContactFile(TestContactStore.TeamFile).Ids);
        var failure = await CallOutcome.FailsAsync(api.ContactCalls.GetContactAsync("2"));
        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task DeleteContact_of_an_ID_the_group_does_not_hold_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.DeleteContactAsync("team", "3"));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
        Assert.Equal(["1", "2"], api.ParsedContactFile(TestContactStore.TeamFile).Ids);
    }

    [Fact]
    public async Task DeleteContact_in_a_group_whose_file_was_skipped_fails_with_FAILED_PRECONDITION()
    {
        await using var api = new ApiHarness();
        api.Files.Seed(api.Clone, BrokenFile, "{ not json");
        api.LoadContacts();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.DeleteContactAsync("broken", "1"));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task DeleteContact_fails_with_FAILED_PRECONDITION_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.DeleteContactAsync("team", "1"));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task MoveGroup_puts_the_group_between_its_new_neighbors_and_returns_the_groups_in_the_new_order()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var reply = await api.ContactCalls.MoveGroupAsync(new V1.MoveGroupRequest { Group = "alumni", PreviousGroup = "me", NextGroup = "team" });

        Assert.Equal(["me", "alumni", "team"], reply.Names());
        Assert.Equal(["me", "alumni", "team"], api.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task MoveGroup_with_no_previous_group_puts_the_group_first()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var reply = await api.ContactCalls.MoveGroupAsync(new V1.MoveGroupRequest { Group = "alumni", NextGroup = "me" });

        Assert.Equal(["alumni", "me", "team"], reply.Names());
    }

    [Fact]
    public async Task MoveGroup_with_no_next_group_puts_the_group_last()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var reply = await api.ContactCalls.MoveGroupAsync(new V1.MoveGroupRequest { Group = "team", PreviousGroup = "alumni" });

        Assert.Equal(["me", "alumni", "team"], reply.Names());
    }

    [Fact]
    public async Task MoveGroup_of_an_unknown_group_fails_with_INVALID_ARGUMENT()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.MoveGroupAsync(new V1.MoveGroupRequest { Group = "nowhere", NextGroup = "me" }));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task MoveGroup_naming_a_group_whose_file_was_skipped_fails_with_FAILED_PRECONDITION_and_changes_nothing()
    {
        await using var api = new ApiHarness();
        SeedWithSkippedGroup(api);
        var before = api.Contacts.Groups.Select(group => group.Name).ToList();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.MoveGroupAsync(new V1.MoveGroupRequest { Group = "broken", NextGroup = "me" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
        Assert.Equal(before, api.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task MoveGroup_next_to_a_group_whose_file_was_skipped_fails_with_FAILED_PRECONDITION_and_changes_nothing()
    {
        await using var api = new ApiHarness();
        SeedWithSkippedGroup(api);
        var before = api.Contacts.Groups.Select(group => group.Name).ToList();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.MoveGroupAsync(new V1.MoveGroupRequest { Group = "team", PreviousGroup = "broken" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
        Assert.Equal(before, api.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task A_failure_of_a_Changed_subscriber_after_SaveContact_wrote_still_returns_the_saved_contact_and_logs_an_Error_with_it()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        var failure = new InvalidOperationException("A subscriber broke.");
        api.Contacts.Changed += (_, _) => throw failure;

        var saved = await api.ContactCalls.SaveContactAsync(Save("team", ContactSeeds.Contact(("id", "9"), ("fullName", "Eva Ilie"))));

        Assert.Equal(ContactSeeds.Map(("id", "9"), ("fullName", "Eva Ilie")), saved.Map());
        Assert.Contains("9", api.ParsedContactFile(TestContactStore.TeamFile).Ids);
        var entry = await api.Log.EntryAsync(LogEventLevel.Error, "SaveContact");
        Assert.IsAssignableFrom<NoticeFailedException>(entry.Exception);
    }

    [Fact]
    public async Task A_failure_of_a_Changed_subscriber_after_DeleteContact_wrote_still_deletes_normally_and_logs_an_Error_with_it()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        api.Contacts.Changed += (_, _) => throw new InvalidOperationException("A subscriber broke.");

        await api.ContactCalls.DeleteContactAsync("team", "2");

        Assert.DoesNotContain("2", api.ParsedContactFile(TestContactStore.TeamFile).Ids);
        var entry = await api.Log.EntryAsync(LogEventLevel.Error, "DeleteContact");
        Assert.IsAssignableFrom<NoticeFailedException>(entry.Exception);
    }

    [Fact]
    public async Task A_successful_SaveContact_logs_the_ID_of_the_contact_and_no_other_contact_data()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        await api.ContactCalls.SaveContactAsync(Save("team", ContactSeeds.Contact(("id", "cd-8812"), ("fullName", "Dana Hidden"), ("title", "Secret Title"))));

        var entry = api.Log.Events.SingleAt(LogEventLevel.Information, "SaveContact", "cd-8812");
        Assert.DoesNotContain("Dana Hidden", entry.EverythingIn());
        Assert.DoesNotContain("Secret Title", entry.EverythingIn());
    }

    private static void SeedWithSkippedGroup(ApiHarness api)
    {
        api.Files.SeedGroup(api.Clone, "team", 1000, ContactFiles.Person("1", "Ana Popa"));
        api.Files.SeedMe(api.Clone, 100, ContactFiles.Person("42", "Me Myself"));
        api.Files.Seed(api.Clone, BrokenFile, "{ not json");
        api.LoadContacts();
    }

    [Fact]
    public async Task MoveGroup_fails_with_FAILED_PRECONDITION_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.MoveGroupAsync(new V1.MoveGroupRequest { Group = "team", NextGroup = "me" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }
}
