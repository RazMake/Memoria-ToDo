// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using Grpc.Core;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>What the Contacts calls that only read answer: groups, the own contact, search, one contact, and a mention's Markdown.</summary>
[Unit]
public class LocalApiContactReadTests
{
    [Fact]
    public async Task ListGroups_returns_every_group_in_display_order_with_its_contacts_by_full_name()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var reply = await api.ContactCalls.ListGroupsAsync();

        Assert.Equal(["me", "team", "alumni"], reply.Names());
        Assert.Equal(["1", "2"], reply.Groups[1].Contacts.Select(contact => contact.Fields["id"]));
    }

    [Fact]
    public async Task ListGroups_gives_each_contact_its_fields_by_file_name_with_the_text_the_file_holds()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var reply = await api.ContactCalls.ListGroupsAsync();

        var ana = reply.Groups.Single(group => group.Name == "team").Contacts[0];
        Assert.Equal(ContactSeeds.Map(("id", "1"), ("fullName", "Ana Popa"), ("alias", "ana"), ("title", "Engineer")), ana.Map());
    }

    [Fact]
    public async Task ListGroups_returns_an_empty_list_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();

        var reply = await api.ContactCalls.ListGroupsAsync();

        Assert.Empty(reply.Groups);
    }

    [Fact]
    public async Task GetMe_returns_the_own_contact()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var me = await api.ContactCalls.GetMeAsync();

        Assert.Equal(ContactSeeds.Map(("id", "42"), ("fullName", "Me Myself"), ("title", "Owner")), me.Map());
    }

    [Fact]
    public async Task GetMe_fails_with_NOT_FOUND_when_there_is_no_own_contact()
    {
        await using var api = new ApiHarness();
        api.Files.SeedGroup(api.Clone, "team", 1000, ContactFiles.Person("1", "Ana Popa"));
        api.LoadContacts();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.GetMeAsync());

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task GetMe_fails_with_NOT_FOUND_when_me_json_was_skipped()
    {
        await using var api = new ApiHarness();
        api.Files.Seed(api.Clone, TestContactStore.MeFile, "{ not json");
        api.LoadContacts();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.GetMeAsync());

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task GetMe_fails_with_NOT_FOUND_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.GetMeAsync());

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task SearchContacts_returns_the_contacts_with_every_word_in_full_name_order()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var engineers = await api.ContactCalls.SearchContactsAsync("engineer");
        var lead = await api.ContactCalls.SearchContactsAsync("ENGINEER lead");

        Assert.Equal(["1", "2"], engineers.Ids());
        Assert.Equal(["2"], lead.Ids());
    }

    [Fact]
    public async Task SearchContacts_returns_an_empty_list_when_nobody_matches()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var reply = await api.ContactCalls.SearchContactsAsync("nobody-has-this");

        Assert.Empty(reply.Contacts);
    }

    [Fact]
    public async Task SearchContacts_returns_an_empty_list_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();

        var reply = await api.ContactCalls.SearchContactsAsync("ana");

        Assert.Empty(reply.Contacts);
    }

    [Theory]
    [InlineData("2", "Bob Rus")]
    [InlineData("ana", "Ana Popa")]
    public async Task GetContact_finds_a_contact_by_its_ID_or_its_alias(string name, string fullName)
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var contact = await api.ContactCalls.GetContactAsync(name);

        Assert.Equal(fullName, contact.Fields["fullName"]);
    }

    [Fact]
    public async Task A_successful_GetContact_logs_the_ID_the_caller_named_and_no_other_contact_data()
    {
        await using var api = new ApiHarness();
        api.Files.SeedGroup(api.Clone, "team", 1000, ContactFiles.Person("ab-4711", "Ana Hidden", ("title", "Secret Title")));
        api.LoadContacts();

        await api.ContactCalls.GetContactAsync("ab-4711");

        var entry = api.Log.Events.SingleAt(LogEventLevel.Information, "GetContact", "ab-4711");
        Assert.DoesNotContain("Ana Hidden", entry.EverythingIn());
        Assert.DoesNotContain("Secret Title", entry.EverythingIn());
    }

    [Fact]
    public async Task GetContact_carries_every_stored_field_as_the_file_writes_it()
    {
        await using var api = new ApiHarness();
        api.Files.SeedDataTypes(api.Clone);
        api.Files.SeedGroup(api.Clone, "team", 1000, ContactFiles.Person("7", "Dan Pop",
            ("alias", "dan"), ("firstName", "Dan"), ("nickname", "Danny"), ("title", "Lead"), ("location", "Cluj"),
            ("levelId", "L5"), ("workingType", "remote"), ("pronounsKey", "she"), ("careerPathKey", "em"), ("reportsTo", "1"),
            ("dayStartTime", "09:30"), ("levelStartDate", "2024-01-31"), ("teamMemberSince", "2023-05-01"), ("exitDate", "2026-12-31")));
        api.LoadContacts();

        var contact = await api.ContactCalls.GetContactAsync("7");

        Assert.Equal(ContactSeeds.Map(
            ("id", "7"), ("alias", "dan"), ("fullName", "Dan Pop"), ("firstName", "Dan"), ("nickname", "Danny"), ("title", "Lead"),
            ("location", "Cluj"), ("levelId", "L5"), ("workingType", "remote"), ("pronounsKey", "she"), ("careerPathKey", "em"),
            ("reportsTo", "1"), ("dayStartTime", "09:30"), ("levelStartDate", "2024-01-31"), ("teamMemberSince", "2023-05-01"),
            ("exitDate", "2026-12-31")), contact.Map());
    }

    [Fact]
    public async Task GetContact_leaves_out_the_nickname_a_contact_does_not_store_instead_of_giving_its_first_name()
    {
        await using var api = new ApiHarness();
        api.Files.SeedGroup(api.Clone, "team", 1000, ContactFiles.Person("1", "Ana Popa", ("firstName", "Ana")));
        api.LoadContacts();

        var contact = await api.ContactCalls.GetContactAsync("1");

        Assert.Equal(ContactSeeds.Map(("id", "1"), ("fullName", "Ana Popa"), ("firstName", "Ana")), contact.Map());
    }

    [Fact]
    public async Task GetContact_of_an_unknown_name_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.GetContactAsync("nobody"));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task GetContact_of_an_alias_two_contacts_hold_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();
        api.Files.SeedGroup(api.Clone, "team", 1000,
            ContactFiles.Person("1", "Ana Popa", ("alias", "pop")), ContactFiles.Person("2", "Bob Pop", ("alias", "pop")));
        api.LoadContacts();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.GetContactAsync("pop"));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task GetContact_fails_with_NOT_FOUND_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.GetContactAsync("1"));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task MentionOf_returns_the_Markdown_link_for_the_contact_and_template()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var mention = await api.ContactCalls.MentionOfAsync("1", "{fullName}");

        Assert.Equal("[Ana Popa](contact:1 \"{fullName}\")", mention.Markdown);
    }

    [Fact]
    public async Task MentionOf_of_an_unknown_contact_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.MentionOfAsync("nobody", "{fullName}"));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task MentionOf_with_an_invalid_template_fails_with_INVALID_ARGUMENT()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        var failure = await CallOutcome.FailsAsync(api.ContactCalls.MentionOfAsync("1", "{nope}"));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task A_Contacts_call_logs_one_Information_entry_with_the_method_the_ID_and_the_status()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();

        await api.ContactCalls.MentionOfAsync("1", "{fullName}");

        var entry = await api.Log.EntryAsync(LogEventLevel.Information, "MentionOf", "1");
        Assert.True(entry.NamesStatus(StatusCode.OK));
        Assert.Single(api.Log.Events, e => e.Names("MentionOf"));
    }

    [Fact]
    public async Task A_Contacts_call_the_program_caused_to_fail_logs_a_Warning_with_the_method_the_ID_and_the_status()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        await CallOutcome.FailsAsync(api.ContactCalls.MentionOfAsync("nobody", "{fullName}"));

        var entry = await api.Log.EntryAsync(LogEventLevel.Warning, "MentionOf", "nobody");

        Assert.True(entry.NamesStatus(StatusCode.NotFound));
    }
}
