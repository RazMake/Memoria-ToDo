using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.MCPServer.Tests;

/// <summary>The tools that read, save, and delete contacts and move contact groups.</summary>
[Unit]
public class McpContactToolsTests
{
    private static McpHarness WithTeam(Func<IContacts, IContacts>? wrapContactsForServer = null)
    {
        var app = new McpHarness(wrapContactsForServer: wrapContactsForServer);
        app.UseContacts(
            ContactFiles.Person("1", "Ana Pop", ("alias", "ana"), ("reportsTo", "2"), ("title", "Engineer")),
            ContactFiles.Person("2", "Bob Ionescu", ("alias", "bob")));
        app.Files.SeedDataTypes(McpHarness.Clone);
        app.Contacts.UseRepositoryFolder(McpHarness.Clone);
        return app;
    }

    [Fact]
    public async Task list_contacts_answers_the_groups_with_their_contacts_and_the_users_own_contact()
    {
        await using var app = WithTeam();
        app.Files.SeedMe(McpHarness.Clone, 100, ContactFiles.Person("42", "Me Myself"));
        app.Contacts.UseRepositoryFolder(McpHarness.Clone);

        var answer = await app.OkAsync("list_contacts");

        var groups = answer.Items("groups");
        var team = groups.Single(group => group.Str("name") == "team");
        Assert.Equal(new[] { "Ana Pop", "Bob Ionescu" }, team.Items("contacts").Select(contact => contact.Str("fullName")));
        Assert.Equal("Me Myself", answer.GetProperty("me").Str("fullName"));
        Assert.Empty(answer.Items("skippedFiles"));
    }

    [Fact]
    public async Task list_contacts_names_the_files_that_could_not_be_read_with_their_reason()
    {
        await using var app = WithTeam();
        app.Files.Seed(McpHarness.Clone, "autocomplete/contacts/broken.json", "this is not json");
        app.Contacts.UseRepositoryFolder(McpHarness.Clone);

        var answer = await app.OkAsync("list_contacts");

        var skipped = Assert.Single(answer.Items("skippedFiles"));
        Assert.Contains("broken.json", skipped.Str("path"));
        Assert.False(string.IsNullOrWhiteSpace(skipped.Str("reason")));
    }

    [Fact]
    public async Task find_contact_answers_the_contact_with_that_alias_or_id()
    {
        await using var app = WithTeam();

        var byAlias = await app.OkAsync("find_contact", new { name = "ana" });
        var byId = await app.OkAsync("find_contact", new { name = "2" });

        Assert.Equal("Ana Pop", byAlias.Str("fullName"));
        Assert.Equal("Bob Ionescu", byId.Str("fullName"));
    }

    [Fact]
    public async Task find_contact_fails_when_no_contact_has_the_name_naming_it()
    {
        await using var app = WithTeam();

        var error = await app.ErrorAsync("find_contact", new { name = "nobody" });

        Assert.Contains("nobody", error);
    }

    [Fact]
    public async Task search_contacts_answers_the_contacts_holding_every_word()
    {
        await using var app = WithTeam();

        var answer = await app.OkAsync("search_contacts", new { text = "engineer ana" });

        Assert.Equal(new[] { "Ana Pop" }, answer.Items("contacts").Select(contact => contact.Str("fullName")));
    }

    [Fact]
    public async Task get_reporting_line_answers_the_manager_and_the_direct_reports()
    {
        await using var app = WithTeam();

        var ofAna = await app.OkAsync("get_reporting_line", new { name = "ana" });
        var ofBob = await app.OkAsync("get_reporting_line", new { name = "bob" });

        Assert.Equal("Bob Ionescu", ofAna.GetProperty("manager").Str("fullName"));
        Assert.Empty(ofAna.Items("directReports"));
        Assert.True(ofBob.IsAbsent("manager"));
        Assert.Equal(new[] { "Ana Pop" }, ofBob.Items("directReports").Select(contact => contact.Str("fullName")));
    }

    [Fact]
    public async Task get_contact_mention_answers_the_markdown_to_put_in_a_description_and_the_templates()
    {
        await using var app = WithTeam();

        var answer = await app.OkAsync("get_contact_mention", new { name = "ana", template = "{fullName}" });

        Assert.StartsWith("[Ana Pop](contact:1", answer.Str("mention"));
        Assert.NotEmpty(answer.Strings("templates"));
    }

    [Fact]
    public async Task get_contact_mention_fails_with_the_modules_message_for_an_invalid_template()
    {
        await using var app = WithTeam();

        var error = await app.ErrorAsync("get_contact_mention", new { name = "ana", template = "{noSuchField" });

        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task save_contact_adds_a_new_contact_to_the_group_and_writes_its_file()
    {
        await using var app = WithTeam();

        var answer = await app.OkAsync("save_contact", new { group = "team", id = "3", fullName = "Cara Dan", alias = "cara", title = "Designer" });

        var saved = app.Contacts.Find("cara");
        Assert.NotNull(saved);
        Assert.Equal("Cara Dan", saved.FullName);
        Assert.Equal("Designer", saved.Title);
        Assert.Equal("Cara Dan", answer.Str("fullName"));
    }

    [Fact]
    public async Task save_contact_replaces_the_contact_with_the_same_id()
    {
        await using var app = WithTeam();

        await app.OkAsync("save_contact", new { group = "team", id = "2", fullName = "Bob Renamed", alias = "bob" });

        Assert.Equal("Bob Renamed", app.Contacts.Find("bob")?.FullName);
    }

    [Fact]
    public async Task save_contact_keeps_the_fields_of_an_existing_contact_that_the_call_leaves_out()
    {
        await using var app = WithTeam();

        var answer = await app.OkAsync("save_contact", new { group = "team", id = "1", fullName = "Ana Renamed" });

        var saved = app.Contacts.Find("ana");
        Assert.NotNull(saved);
        Assert.Equal("Ana Renamed", saved.FullName);
        Assert.Equal("Engineer", saved.Title);
        Assert.Equal("2", saved.ReportsTo);
        Assert.Equal("Engineer", answer.Str("title"));
    }

    [Fact]
    public async Task save_contact_keeps_the_current_full_name_and_the_changed_field_when_the_call_leaves_the_full_name_out()
    {
        await using var app = WithTeam();

        var answer = await app.OkAsync("save_contact", new { group = "team", id = "1", title = "Lead" });

        var saved = app.Contacts.Find("ana");
        Assert.NotNull(saved);
        Assert.Equal("Ana Pop", saved.FullName);
        Assert.Equal("Lead", saved.Title);
        Assert.Equal("2", saved.ReportsTo);
        Assert.Equal("Ana Pop", answer.Str("fullName"));
        Assert.Equal("Lead", answer.Str("title"));
    }

    [Fact]
    public async Task save_contact_refuses_a_new_contact_with_no_full_name_and_adds_nothing()
    {
        await using var app = WithTeam();

        var error = await app.ErrorAsync("save_contact", new { group = "team", id = "9", alias = "nine" });

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Null(app.Contacts.Find("9"));
        Assert.Null(app.Contacts.Find("nine"));
    }

    [Fact]
    public async Task save_contact_keeps_what_another_writer_saved_just_before_it_wrote()
    {
        await using var app = WithTeam(contacts => WriterBeforeSave.Around(contacts, inner =>
        {
            var ana = inner.Find("ana")!;
            inner.Save("team", ana with { Title = "Lead" });
        }));

        var answer = await app.OkAsync("save_contact", new { group = "team", id = "1", fullName = "Ana Renamed" });

        var saved = app.Contacts.Find("ana");
        Assert.NotNull(saved);
        Assert.Equal("Ana Renamed", saved.FullName);
        Assert.Equal("Lead", saved.Title);
        Assert.Equal("2", saved.ReportsTo);
        Assert.Equal("Lead", answer.Str("title"));
    }

    [Fact]
    public async Task save_contact_changes_only_the_fields_it_is_given_on_an_existing_contact()
    {
        await using var app = WithTeam();

        await app.OkAsync("save_contact", new { group = "team", id = "1", fullName = "Ana Pop", title = "Lead" });

        var saved = app.Contacts.Find("ana");
        Assert.NotNull(saved);
        Assert.Equal("Lead", saved.Title);
        Assert.Equal("2", saved.ReportsTo);
    }

    [Fact]
    public async Task save_contact_clears_a_text_field_given_empty_and_keeps_the_others()
    {
        await using var app = WithTeam();

        await app.OkAsync("save_contact", new { group = "team", id = "1", fullName = "Ana Pop", title = "" });

        var saved = app.Contacts.Find("ana");
        Assert.NotNull(saved);
        Assert.True(string.IsNullOrEmpty(saved.Title));
        Assert.Equal("2", saved.ReportsTo);
    }

    [Fact]
    public async Task save_contact_clears_a_time_and_a_date_given_empty()
    {
        await using var app = new McpHarness();
        app.UseContacts(ContactFiles.Person("1", "Ana Pop", ("alias", "ana"), ("dayStartTime", "09:30"), ("levelStartDate", "2025-03-01")));

        await app.OkAsync("save_contact", new { group = "team", id = "1", fullName = "Ana Pop", dayStartTime = "", levelStartDate = "" });

        var saved = app.Contacts.Find("ana");
        Assert.NotNull(saved);
        Assert.Null(saved.DayStartTime);
        Assert.Null(saved.LevelStartDate);
    }

    [Fact]
    public async Task list_contacts_answers_the_data_types_with_their_keys()
    {
        await using var app = WithTeam();

        var answer = await app.OkAsync("list_contacts");

        var types = answer.Items("dataTypes");
        var pronouns = types.Single(type => type.Str("keyField") == "pronounsKey");
        var careerPaths = types.Single(type => type.Str("keyField") == "careerPathKey");
        Assert.Equal(new[] { "he", "she" }, pronouns.Items("entries").Select(entry => entry.Str("id")));
        Assert.Equal(new[] { "em", "se" }, careerPaths.Items("entries").Select(entry => entry.Str("id")));
    }

    [Fact]
    public async Task get_contact_mention_uses_the_first_of_several_configured_templates_when_none_is_named()
    {
        await using var app = new McpHarness();
        app.Contacts.UseContacts(app.Files, McpHarness.Clone, ["{alias}", "{fullName}"], ContactFiles.Person("1", "Ana Pop", ("alias", "ana")));

        var answer = await app.OkAsync("get_contact_mention", new { name = "ana" });

        Assert.StartsWith("[ana](contact:1", answer.Str("mention"));
        Assert.Equal(new[] { "{alias}", "{fullName}" }, answer.Strings("templates"));
    }

    [Fact]
    public async Task save_contact_fails_with_the_modules_message_for_an_unknown_group_and_saves_nothing()
    {
        await using var app = WithTeam();

        var error = await app.ErrorAsync("save_contact", new { group = "no-such-group", id = "9", fullName = "Nobody Here" });

        Assert.Contains("no-such-group", error);
        Assert.Null(app.Contacts.Find("9"));
    }

    [Fact]
    public async Task save_contact_fails_with_the_modules_message_for_a_name_another_contact_holds()
    {
        await using var app = WithTeam();

        var error = await app.ErrorAsync("save_contact", new { group = "team", id = "3", fullName = "Cara Dan", alias = "ana" });

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Null(app.Contacts.Find("3"));
    }

    [Fact]
    public async Task delete_contact_removes_the_contact_from_the_group()
    {
        await using var app = WithTeam();

        await app.OkAsync("delete_contact", new { group = "team", id = "2" });

        Assert.Null(app.Contacts.Find("bob"));
        Assert.NotNull(app.Contacts.Find("ana"));
    }

    [Fact]
    public async Task delete_contact_fails_for_an_id_the_group_does_not_hold_naming_it()
    {
        await using var app = WithTeam();

        var error = await app.ErrorAsync("delete_contact", new { group = "team", id = "77" });

        Assert.Contains("77", error);
    }

    [Fact]
    public async Task move_contact_group_moves_the_group_between_its_neighbors()
    {
        await using var app = WithTeam();
        app.Files.SeedGroup(McpHarness.Clone, "alpha", 100, ContactFiles.Person("10", "Alpha One"));
        app.Files.SeedGroup(McpHarness.Clone, "beta", 200, ContactFiles.Person("11", "Beta One"));
        app.Contacts.UseRepositoryFolder(McpHarness.Clone);

        await app.OkAsync("move_contact_group", new { group = "beta", nextGroup = "alpha" });

        var names = app.Contacts.Groups.Select(group => group.Name).ToList();
        Assert.True(names.IndexOf("beta") < names.IndexOf("alpha"));
    }

    [Fact]
    public async Task move_contact_group_fails_for_an_unknown_group_naming_it()
    {
        await using var app = WithTeam();

        var error = await app.ErrorAsync("move_contact_group", new { group = "no-such-group" });

        Assert.Contains("no-such-group", error);
    }

    [Theory]
    [InlineData("list_contacts")]
    [InlineData("find_contact")]
    [InlineData("search_contacts")]
    public async Task Contact_reads_fail_with_a_message_to_choose_a_repository_when_no_clone_is_selected(string tool)
    {
        await using var app = new McpHarness(cloneSelected: false);

        var error = await app.ErrorAsync(tool, new { name = "ana", text = "ana" });

        Assert.Contains("Configure", error);
    }

    [Theory]
    [InlineData("save_contact")]
    [InlineData("delete_contact")]
    public async Task Contact_changes_fail_with_a_message_to_choose_a_repository_when_no_clone_is_selected(string tool)
    {
        await using var app = new McpHarness(cloneSelected: false);

        var error = await app.ErrorAsync(tool, new { group = "team", id = "1", fullName = "Ana Pop" });

        Assert.Contains("Configure", error);
    }
}
