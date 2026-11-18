using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>The mention templates file: which templates it offers, and when it is skipped or reported.</summary>
[Unit]
public sealed class TemplateFileTests
{
    private static readonly string[] Default = ["{fullName}"];

    private readonly ContactsFixture f = new();

    private void LoadWithContacts()
    {
        f.SeedGroup("team", 1000);
        f.SeedDataTypes();
        f.Load();
    }

    [Fact]
    public void Keeps_the_order_of_the_file()
    {
        f.Seed(ContactsFixture.TemplatesFile, ContactFiles.Templates("{firstName}", "{fullName} ({title})", "@{alias}"));

        LoadWithContacts();

        Assert.Equal(["{firstName}", "{fullName} ({title})", "@{alias}"], f.Contacts.MentionTemplates);
        Assert.Empty(f.Contacts.SkippedFiles);
    }

    [Fact]
    public void Offers_the_default_when_the_file_is_missing_without_reporting_it()
    {
        LoadWithContacts();

        Assert.Equal(Default, f.Contacts.MentionTemplates);
        Assert.Empty(f.Contacts.SkippedFiles);
    }

    [Fact]
    public void Offers_the_default_when_the_file_is_empty_without_reporting_it()
    {
        f.SeedBytes(ContactsFixture.TemplatesFile, []);

        LoadWithContacts();

        Assert.Equal(Default, f.Contacts.MentionTemplates);
        Assert.Empty(f.Contacts.SkippedFiles);
    }

    [Fact]
    public void Skips_a_file_from_a_newer_version_and_asks_for_an_update()
    {
        const string content = """{ "version": 2, "templates": [ "{firstName}" ] }""";
        f.Seed(ContactsFixture.TemplatesFile, content);

        LoadWithContacts();

        Assert.Equal(Default, f.Contacts.MentionTemplates);
        var skipped = Assert.Single(f.Contacts.SkippedFiles, file => file.RelativePath == ContactsFixture.TemplatesFile);
        Assert.True(skipped.RequiresUpdate);
        Assert.Equal(content, f.Text(ContactsFixture.TemplatesFile));
    }

    [Theory]
    [InlineData("""{ "templates": [ "{firstName}" ] }""")]
    [InlineData("""{ "version": 1.5, "templates": [ "{firstName}" ] }""")]
    [InlineData("""{ "version": "1", "templates": [ "{firstName}" ] }""")]
    [InlineData("""{ "version": 1 }""")]
    [InlineData("""{ "version": 1, "templates": "{firstName}" }""")]
    [InlineData("""{ "version": 1, "templates": [ 5 ] }""")]
    [InlineData("""[ "{firstName}" ]""")]
    [InlineData("{ not json")]
    public void Reports_a_file_that_is_not_a_templates_file_and_offers_the_default(string content)
    {
        f.Seed(ContactsFixture.TemplatesFile, content);

        LoadWithContacts();

        Assert.Equal(Default, f.Contacts.MentionTemplates);
        f.AssertSkipped(ContactsFixture.TemplatesFile);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{nope}")]
    [InlineData("{fullName")]
    [InlineData("fullName}")]
    [InlineData("{careerPath.}")]
    public void Reports_a_file_with_an_invalid_template_and_offers_the_default(string template)
    {
        f.Seed(ContactsFixture.TemplatesFile, ContactFiles.Templates("{firstName}", template));

        LoadWithContacts();

        Assert.Equal(Default, f.Contacts.MentionTemplates);
        f.AssertSkipped(ContactsFixture.TemplatesFile);
    }

    [Fact]
    public void Reports_a_file_with_a_lone_surrogate_in_a_template_and_offers_the_default()
    {
        f.Seed(ContactsFixture.TemplatesFile, """{ "version": 1, "templates": [ "{firstName}", "{fullName}\uD800" ] }""");

        LoadWithContacts();

        Assert.Equal(Default, f.Contacts.MentionTemplates);
        f.AssertSkipped(ContactsFixture.TemplatesFile);
    }

    [Fact]
    public void Reports_a_file_that_can_not_be_read_and_offers_the_default()
    {
        f.Seed(ContactsFixture.TemplatesFile, ContactFiles.Templates("{firstName}"));
        f.Disk.FailReadsUnder(f.PathOf(ContactsFixture.Folder + "/dataTypes"));

        LoadWithContacts();

        Assert.Equal(Default, f.Contacts.MentionTemplates);
        f.AssertSkipped(ContactsFixture.TemplatesFile);
    }

    [Fact]
    public void Offers_the_default_when_no_clone_is_selected()
    {
        Assert.Equal(Default, f.Contacts.MentionTemplates);
    }
}
