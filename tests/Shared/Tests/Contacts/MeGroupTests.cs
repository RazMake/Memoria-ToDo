using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>me.json: the user's own contact, in the group "me".</summary>
[Unit]
public sealed class MeGroupTests
{
    private readonly ContactsFixture f = new();

    [Fact]
    public void Loads_me_json_as_the_group_me_with_its_one_contact()
    {
        f.SeedMe(100, ContactFiles.Person("42", "Me Myself", ("title", "Owner")));

        f.Load();

        var group = f.Group("me");
        Assert.Equal(100, group.Position);
        Assert.Equal([new Contact("42", "Me Myself", Title: "Owner")], group.Members);
    }

    [Fact]
    public void Gives_the_group_s_contact_as_Me()
    {
        f.SeedMe(100, ContactFiles.Person("42", "Me Myself"));

        f.Load();

        Assert.Equal(new Contact("42", "Me Myself"), f.Contacts.Me);
    }

    [Fact]
    public void Loads_Me_json_as_the_user_s_own_group_named_Me()
    {
        f.Seed(ContactsFixture.Folder + "/Me.json", ContactFiles.Me(100, ContactFiles.Person("42", "Me Myself")));

        f.Load();

        Assert.Equal(new Contact("42", "Me Myself"), f.Contacts.Me);
        Assert.Equal([new Contact("42", "Me Myself")], f.Group("Me").Members);
        Assert.False(f.Group("Me").IsTeam);
    }

    [Fact]
    public void Skips_both_files_and_has_no_Me_when_me_json_and_Me_json_both_exist()
    {
        f.Seed(ContactsFixture.MeFile, ContactFiles.Me(100, ContactFiles.Person("42", "Me Myself")));
        f.Seed(ContactsFixture.Folder + "/Me.json", ContactFiles.Me(200, ContactFiles.Person("43", "Me Too")));

        f.Load();

        Assert.Null(f.Contacts.Me);
        Assert.Empty(f.Contacts.Groups);
        f.AssertSkipped(ContactsFixture.MeFile);
        f.AssertSkipped(ContactsFixture.Folder + "/Me.json");
    }

    [Theory]
    [InlineData("me", "Me", "{ not json")]
    [InlineData("Me", "me", "{ not json")]
    [InlineData("me", "Me", """{ "version": 2, "position": 200, "details": { "id": "43", "fullName": "Me Too" } }""")]
    [InlineData("Me", "me", """{ "version": 2, "position": 200, "details": { "id": "43", "fullName": "Me Too" } }""")]
    public void Skips_both_files_and_has_no_Me_when_one_own_group_file_is_valid_and_the_other_cannot_be_read(string valid, string unreadable, string unreadableText)
    {
        f.Seed($"{ContactsFixture.Folder}/{valid}.json", ContactFiles.Me(100, ContactFiles.Person("42", "Me Myself")));
        f.Seed($"{ContactsFixture.Folder}/{unreadable}.json", unreadableText);
        f.SeedDataTypes();
        f.ForgetWrites();

        f.Load();

        Assert.Null(f.Contacts.Me);
        Assert.Empty(f.Contacts.Groups);
        Assert.Equal(
            [$"{ContactsFixture.Folder}/Me.json", ContactsFixture.MeFile],
            f.SkippedPaths.Order(StringComparer.Ordinal));
        Assert.Throws<SkippedFileException>(() => f.Contacts.Save(valid, new Contact("42", "Me Myself")));
        f.AssertNothingWritten();
    }

    [Fact]
    public void Has_no_Me_when_me_json_is_missing()
    {
        f.SeedGroup("team", 1000);

        f.Load();

        Assert.Null(f.Contacts.Me);
        Assert.DoesNotContain(f.Contacts.Groups, group => group.Name == "me");
    }

    [Fact]
    public void Has_no_Me_when_me_json_holds_a_lone_surrogate_and_reports_it()
    {
        f.SeedDataTypes();
        f.Seed(ContactsFixture.MeFile, ContactFiles.Me(100, """{ "id": "42", "fullName": "Me\uD800" }"""));

        f.Load();

        Assert.Null(f.Contacts.Me);
        Assert.Empty(f.Contacts.Groups);
        f.AssertSkipped(ContactsFixture.MeFile);
    }

    [Fact]
    public void Has_no_Me_when_me_json_is_skipped()
    {
        f.SeedDataTypes();

        f.Seed(ContactsFixture.MeFile, ContactFiles.Me(100, """{ "fullName": "No id" }"""));

        f.Load();

        Assert.Null(f.Contacts.Me);
        Assert.Empty(f.Contacts.Groups);
        Assert.Equal([ContactsFixture.MeFile], f.SkippedPaths);
    }
}
