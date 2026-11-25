using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Moving a template first re-reads the templates file under the lock, so a change on disk since the latest load is kept or, when unusable, stops the move.</summary>
[Unit]
public sealed class MoveTemplateFirstReReadTests
{
    private const string File = ContactsFixture.TemplatesFile;

    private readonly ContactsFixture f = new();

    public MoveTemplateFirstReReadTests()
    {
        f.Seed(File, ContactFiles.Templates("{fullName}", "{firstName}"));
        f.LoadWithContacts();
    }

    [Fact]
    public void Keeps_a_template_added_on_disk_since_the_latest_load_and_applies_the_move_to_the_fresh_content()
    {
        f.Seed(File, ContactFiles.Templates("{fullName}", "{firstName}", "{title}"));

        f.Contacts.MoveTemplateFirst("{firstName}");

        Assert.Equal(["{firstName}", "{fullName}", "{title}"], f.TemplatesInFile());
        Assert.Equal(["{firstName}", "{fullName}", "{title}"], f.Contacts.MentionTemplates);
    }

    [Fact]
    public void Keeps_the_file_as_read_and_raises_only_Changed_when_the_fresh_file_no_longer_lists_the_template()
    {
        var edited = ContactFiles.Templates("{fullName}", "{title}");
        f.Seed(File, edited);
        var notices = f.Notices.Count;

        f.Contacts.MoveTemplateFirst("{firstName}");

        f.AssertNothingWritten();
        Assert.Equal(["Changed"], f.Notices.Skip(notices));
        Assert.Equal(["{fullName}", "{title}"], f.Contacts.MentionTemplates);
        Assert.Equal(edited, f.Text(File));
    }

    [Fact]
    public void Keeps_a_hand_edit_and_raises_only_Changed_when_the_template_is_now_first_in_the_fresh_file()
    {
        var edited = ContactFiles.Templates("{firstName}", "{fullName}", "{title}");
        f.Seed(File, edited);
        var notices = f.Notices.Count;

        f.Contacts.MoveTemplateFirst("{firstName}");

        f.AssertNothingWritten();
        Assert.Equal(["Changed"], f.Notices.Skip(notices));
        Assert.Equal(["{firstName}", "{fullName}", "{title}"], f.Contacts.MentionTemplates);
        Assert.Equal(edited, f.Text(File));
    }

    [Fact]
    public void Raises_nothing_when_the_fresh_file_lists_the_same_templates_as_the_list()
    {
        f.Seed(File, ContactFiles.Templates("{fullName}", "{firstName}"));
        var notices = f.Notices.Count;

        f.Contacts.MoveTemplateFirst("{fullName}");

        f.AssertNothingWritten();
        Assert.Equal(notices, f.Notices.Count);
        Assert.Equal(["{fullName}", "{firstName}"], f.Contacts.MentionTemplates);
    }

    [Fact]
    public void Falls_back_to_the_default_list_and_raises_only_Changed_when_the_file_is_now_missing()
    {
        f.Disk.DeleteFile(f.PathOf(File));
        var notices = f.Notices.Count;

        f.Contacts.MoveTemplateFirst("{firstName}");

        f.AssertNothingWritten();
        Assert.Equal(["Changed"], f.Notices.Skip(notices));
        Assert.Equal(["{fullName}"], f.Contacts.MentionTemplates);
        Assert.False(f.Exists(File));
    }

    [Fact]
    public void Falls_back_to_the_default_list_and_raises_only_Changed_when_the_file_is_now_empty()
    {
        f.SeedBytes(File, []);
        var notices = f.Notices.Count;

        f.Contacts.MoveTemplateFirst("{firstName}");

        f.AssertNothingWritten();
        Assert.Equal(["Changed"], f.Notices.Skip(notices));
        Assert.Equal(["{fullName}"], f.Contacts.MentionTemplates);
        Assert.Empty(f.Bytes(File));
    }

    [Fact]
    public void Does_not_re_read_a_file_the_latest_load_skipped_so_the_list_stays_the_default_and_nothing_is_raised()
    {
        var skipping = new ContactsFixture();
        skipping.Seed(File, "{ not json");
        skipping.LoadWithContacts();
        skipping.Seed(File, ContactFiles.Templates("{firstName}", "{fullName}"));
        var notices = skipping.Notices.Count;

        skipping.Contacts.MoveTemplateFirst("{fullName}");

        skipping.AssertNothingWritten();
        Assert.Equal(notices, skipping.Notices.Count);
        Assert.Equal(["{fullName}"], skipping.Contacts.MentionTemplates);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "version": 1, "templates": [ "{fullName}", "{firstName}", "{nope}" ] }""")]
    [InlineData("""{ "version": 2, "templates": [ "{fullName}", "{firstName}" ] }""")]
    public void Throws_InvalidDataException_and_writes_nothing_when_the_fresh_file_can_not_be_read_or_needs_a_newer_app(string content)
    {
        f.Seed(File, content);
        var notices = f.Notices.Count;

        Assert.Throws<InvalidDataException>(() => f.Contacts.MoveTemplateFirst("{firstName}"));

        f.AssertNothingWritten();
        Assert.Equal(notices, f.Notices.Count);
        Assert.Equal(content, f.Text(File));
        Assert.Equal(["{fullName}", "{firstName}"], f.Contacts.MentionTemplates);
        Assert.Empty(f.SkippedPaths);
    }
}
