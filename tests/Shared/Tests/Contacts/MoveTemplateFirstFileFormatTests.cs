using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>How moving a template first writes the templates file: the platform's JSON style, the app's version, then the templates, then the file's other fields.</summary>
[Unit]
public sealed class MoveTemplateFirstFileFormatTests
{
    private const string File = ContactsFixture.TemplatesFile;

    private readonly ContactsFixture f = new();

    private void Load(string content)
    {
        f.Seed(File, content);
        f.LoadWithContacts();
    }

    [Fact]
    public void Writes_in_the_platform_s_JSON_style()
    {
        Load(ContactFiles.Templates("{fullName}", "{firstName}"));

        f.Contacts.MoveTemplateFirst("{firstName}");

        Assert.Equal("{\n  \"version\": 1,\n  \"templates\": [\n    \"{firstName}\",\n    \"{fullName}\"\n  ]\n}", f.Text(File));
    }

    [Fact]
    public void Rewrites_a_hand_written_file_as_version_then_templates_then_the_other_fields_in_file_order_keeping_their_values()
    {
        Load("{\"templates\":[\"{fullName}\",\n\n   \"{firstName}\"],   \"extra\": \"x\",\"count\":7,\n \"version\": 1}");

        f.Contacts.MoveTemplateFirst("{firstName}");

        Assert.Equal(["version", "templates", "extra", "count"], f.TopNames(File));
        Assert.Equal(["{firstName}", "{fullName}"], f.TemplatesInFile());
        var top = f.Parsed(File).Top;
        Assert.Equal("1", top["version"]);
        Assert.Equal("x", top["extra"]);
        Assert.Equal("7", top["count"]);
    }
}
