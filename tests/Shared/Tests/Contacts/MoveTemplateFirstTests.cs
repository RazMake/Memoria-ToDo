using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Moving a template first puts it at the top of the templates file, so the "@" list starts on it, and writes nothing when there is nothing to move.</summary>
[Unit]
public sealed class MoveTemplateFirstTests
{
    private const string File = ContactsFixture.TemplatesFile;

    private readonly ContactsFixture f = new();

    private void Load(params string[] templates)
    {
        f.Seed(File, ContactFiles.Templates(templates));
        f.LoadWithContacts();
    }

    private void AssertNothingChanged(Action move)
    {
        var notices = f.Notices.Count;
        var before = f.Disk.FileExists(f.PathOf(File)) ? f.Bytes(File) : null;

        move();

        f.AssertNothingWritten();
        Assert.Equal(notices, f.Notices.Count);
        Assert.Equal(before, f.Disk.FileExists(f.PathOf(File)) ? f.Bytes(File) : null);
    }

    [Fact]
    public void Moves_the_template_to_the_top_keeping_the_others_in_order()
    {
        Load("{fullName}", "{firstName}", "{title}");

        f.Contacts.MoveTemplateFirst("{title}");

        Assert.Equal(["{title}", "{fullName}", "{firstName}"], f.TemplatesInFile());
        Assert.Equal([File], f.Written);
    }

    [Fact]
    public void Offers_the_new_order_afterwards()
    {
        Load("{fullName}", "{firstName}", "{title}");

        f.Contacts.MoveTemplateFirst("{title}");

        Assert.Equal(["{title}", "{fullName}", "{firstName}"], f.Contacts.MentionTemplates);
    }

    [Fact]
    public void Moves_the_first_match_only_when_the_file_lists_the_template_twice()
    {
        Load("{fullName}", "{firstName}", "{title}", "{firstName}");

        f.Contacts.MoveTemplateFirst("{firstName}");

        Assert.Equal(["{firstName}", "{fullName}", "{title}", "{firstName}"], f.TemplatesInFile());
    }

    [Fact]
    public void Matches_the_template_exactly_including_case()
    {
        Load("Mr {fullName}", "mr {fullName}");

        f.Contacts.MoveTemplateFirst("mr {fullName}");

        Assert.Equal(["mr {fullName}", "Mr {fullName}"], f.TemplatesInFile());
    }

    [Fact]
    public void Raises_Changed_then_reports_the_local_save()
    {
        Load("{fullName}", "{firstName}");
        var before = f.Notices.Count;

        f.Contacts.MoveTemplateFirst("{firstName}");

        Assert.Equal(["Changed", "LocalSaved"], f.Notices.Skip(before));
    }

    [Fact]
    public void Logs_the_move_as_information_naming_the_templates_file()
    {
        Load("{fullName}", "{firstName}");
        var before = f.Logs.Events.Count;

        f.Contacts.MoveTemplateFirst("{firstName}");

        f.Logs.After(before).SingleAt(LogEventLevel.Information, "mentionTemplates.json");
    }

    [Fact]
    public void Writes_nothing_and_raises_nothing_when_the_template_is_already_first()
    {
        Load("{fullName}", "{firstName}");

        AssertNothingChanged(() => f.Contacts.MoveTemplateFirst("{fullName}"));
    }

    [Fact]
    public void Writes_nothing_and_never_creates_the_file_when_it_is_missing()
    {
        f.LoadWithContacts();

        AssertNothingChanged(() => f.Contacts.MoveTemplateFirst("{firstName}"));

        Assert.False(f.Exists(File));
    }

    [Fact]
    public void Writes_nothing_when_the_file_is_empty()
    {
        f.SeedBytes(File, []);
        f.LoadWithContacts();

        AssertNothingChanged(() => f.Contacts.MoveTemplateFirst("{firstName}"));

        Assert.Empty(f.Bytes(File));
    }

    [Theory]
    [InlineData("""{ "version": 1, "templates": [ "{fullName}", "{nope}" ] }""")]
    [InlineData("""{ "version": 2, "templates": [ "{fullName}", "{firstName}" ] }""")]
    public void Writes_nothing_when_the_latest_load_skipped_the_file(string content)
    {
        f.Seed(File, content);
        f.LoadWithContacts();

        AssertNothingChanged(() => f.Contacts.MoveTemplateFirst("{firstName}"));

        Assert.Equal(content, f.Text(File));
    }
}
