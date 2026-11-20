using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Merge of the mention templates file, which is taken whole from one side.</summary>
[Unit]
public sealed class TemplatesMergeTests
{
    private const string Path = ContactsFixture.TemplatesFile;

    private readonly ContactsFixture f = new();

    private static byte[] Templates(params string[] templates) => ContactFiles.Utf8(ContactFiles.Templates(templates));

    [Fact]
    public void Takes_the_side_that_changed()
    {
        var basis = Templates("{fullName}");
        var changed = Templates("{fullName}", "{firstName}");

        Assert.Equal(changed, f.Rules.Merge(Path, basis, changed, basis));
        Assert.Equal(changed, f.Rules.Merge(Path, basis, basis, changed));
    }

    [Fact]
    public void Takes_the_remote_file_when_both_sides_changed()
    {
        var basis = Templates("{fullName}");
        var local = Templates("{title}");
        var remote = Templates("{firstName}");

        Assert.Equal(remote, f.Rules.Merge(Path, basis, local, remote));
    }

    [Fact]
    public void Takes_the_remote_file_when_there_is_no_basis()
    {
        var local = Templates("{title}");
        var remote = Templates("{firstName}");

        Assert.Equal(remote, f.Rules.Merge(Path, null, local, remote));
    }

    [Fact]
    public void Returns_the_winning_bytes_unchanged()
    {
        var basis = Templates("{fullName}");
        var local = ContactFiles.Utf8("{   \"version\": 1,\n\n \"templates\": [\"{title}\"]   }");

        Assert.Equal(local, f.Rules.Merge(Path, basis, local, basis));
    }

    [Fact]
    public void Keeps_a_file_edited_on_one_side_and_deleted_on_the_other()
    {
        var basis = Templates("{fullName}");
        var edited = Templates("{title}");

        Assert.Equal(edited, f.Rules.Merge(Path, basis, edited, null));
        Assert.Equal(edited, f.Rules.Merge(Path, basis, null, edited));
    }

    [Fact]
    public void Deletes_a_file_deleted_on_one_side_and_unchanged_on_the_other()
    {
        var basis = Templates("{fullName}");

        Assert.Null(f.Rules.Merge(Path, basis, null, basis));
        Assert.Null(f.Rules.Merge(Path, basis, basis, null));
    }

    [Fact]
    public void Gives_null_when_every_side_is_absent()
    {
        Assert.Null(f.Rules.Merge(Path, null, null, null));
    }

    [Fact]
    public void Counts_an_empty_side_as_a_file()
    {
        var basis = Templates("{fullName}");

        var merged = f.Rules.Merge(Path, basis, [], basis);
        Assert.NotNull(merged);
        Assert.Equal([], merged);
        Assert.Equal(basis, f.Rules.Merge(Path, [], basis, []));
    }
}

/// <summary>A side Merge can't read stops the merge instead of guessing.</summary>
[Unit]
public sealed class MergeFailureTests
{
    private static readonly byte[] GoodGroup = ContactFiles.Utf8(ContactFiles.Group(1000, ContactFiles.Person("1", "Ana")));
    private static readonly byte[] GoodDataType = ContactFiles.Utf8(ContactFiles.DataType("careerPaths", ContactFiles.Object(("id", "a"))));
    private static readonly byte[] GoodTemplates = ContactFiles.Utf8(ContactFiles.Templates("{fullName}"));

    private readonly ContactsFixture f = new();

    public static TheoryData<string, string> Kinds() => new()
    {
        { ContactsFixture.TeamFile, "group" },
        { ContactsFixture.CareerPathsFile, "dataType" },
        { ContactsFixture.TemplatesFile, "templates" },
    };

    private static byte[] Good(string kind) => kind switch
    {
        "group" => GoodGroup,
        "dataType" => GoodDataType,
        _ => GoodTemplates,
    };

    private static byte[] Newer(string kind) => kind switch
    {
        "group" => ContactFiles.Utf8("""{ "version": 2, "position": 1000, "members": [] }"""),
        "templates" => ContactFiles.Utf8("""{ "version": 2, "templates": [] }"""),
        _ => ContactFiles.Utf8("""{ "version": 2, "careerPaths": [] }"""),
    };

    private static byte[] Broken(string kind) => ContactFiles.Utf8(kind == "dataType" ? ContactFiles.DataType("careerPaths", """{ "name": "no id" }""") : "{ not json");

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Throws_for_a_present_side_that_is_unreadable(string path, string kind)
    {
        var good = Good(kind);
        var broken = Broken(kind);

        Assert.Throws<InvalidDataException>(() => f.Rules.Merge(path, broken, good, good));
        Assert.Throws<InvalidDataException>(() => f.Rules.Merge(path, good, broken, good));
        Assert.Throws<InvalidDataException>(() => f.Rules.Merge(path, good, good, broken));
    }

    [Theory]
    [InlineData(ContactsFixture.TeamFile, "group")]
    [InlineData(ContactsFixture.CareerPathsFile, "dataType")]
    [InlineData(ContactsFixture.TemplatesFile, "templates")]
    public void Throws_for_a_present_side_from_a_newer_version(string path, string kind)
    {
        var good = Good(kind);
        var newer = Newer(kind);

        Assert.Throws<InvalidDataException>(() => f.Rules.Merge(path, newer, good, good));
        Assert.Throws<InvalidDataException>(() => f.Rules.Merge(path, good, newer, good));
        Assert.Throws<InvalidDataException>(() => f.Rules.Merge(path, good, good, newer));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Does_not_change_the_bytes_it_is_given_and_never_uses_the_file_system(string path, string kind)
    {
        var basis = Good(kind);
        var local = Good(kind).ToArray();
        var remote = Good(kind).ToArray();
        var before = (basis: basis.ToArray(), local: local.ToArray(), remote: remote.ToArray());
        f.ForgetWrites();

        f.Rules.Holds(path);
        f.Rules.Merge(path, basis, local, remote);

        Assert.Equal(before.basis, basis);
        Assert.Equal(before.local, local);
        Assert.Equal(before.remote, remote);
        Assert.Equal(0, f.Files.Calls);
    }
}
