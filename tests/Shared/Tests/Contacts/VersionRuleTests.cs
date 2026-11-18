using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>The version every group, me, and templates file needs: a whole number from 1 up, and no newer than this app reads.</summary>
[Unit]
public sealed class VersionRuleTests
{
    private readonly ContactsFixture f = new();

    public VersionRuleTests() => f.SeedDataTypes();

    /// <summary>Seeds a file of the kind with <paramref name="versionPart"/> (such as <c>"version": 1,</c>) at its top, and returns its path.</summary>
    private string Seed(string kind, string versionPart)
    {
        var (path, body) = kind switch
        {
            "group" => (ContactsFixture.TeamFile, """ "position": 1000, "members": [ { "id": "1", "fullName": "Ana" } ] """),
            "me" => (ContactsFixture.MeFile, """ "position": 100, "details": { "id": "1", "fullName": "Ana" } """),
            _ => (ContactsFixture.TemplatesFile, """ "templates": [ "{firstName}" ] """),
        };
        f.Seed(path, "{ " + versionPart + body + " }");
        return path;
    }

    private void AssertLoaded(string kind)
    {
        switch (kind)
        {
            case "group": Assert.Equal(["1"], ContactsFixture.Ids(f.Group("team").Members)); break;
            case "me": Assert.Equal("1", f.Contacts.Me?.Id); break;
            default: Assert.Equal(["{firstName}"], f.Contacts.MentionTemplates); break;
        }
    }

    [Theory]
    [InlineData("group", "")]
    [InlineData("me", "")]
    [InlineData("templates", "")]
    [InlineData("group", "\"version\": null,")]
    [InlineData("me", "\"version\": null,")]
    [InlineData("templates", "\"version\": null,")]
    public void Skips_a_file_without_a_version(string kind, string versionPart)
    {
        var path = Seed(kind, versionPart);

        f.Load();

        Assert.Equal([path], f.SkippedPaths);
        Assert.False(f.Contacts.SkippedFiles.Single().RequiresUpdate);
    }

    [Theory]
    [InlineData("group", "1.5")]
    [InlineData("me", "1.5")]
    [InlineData("templates", "1.5")]
    [InlineData("group", "\"1\"")]
    [InlineData("me", "\"1\"")]
    [InlineData("templates", "\"1\"")]
    [InlineData("group", "0")]
    [InlineData("group", "-1")]
    public void Skips_a_file_whose_version_is_not_a_whole_number_from_1_up(string kind, string version)
    {
        var path = Seed(kind, $"\"version\": {version},");

        f.Load();

        Assert.Equal([path], f.SkippedPaths);
        Assert.False(f.Contacts.SkippedFiles.Single().RequiresUpdate);
    }

    [Theory]
    [InlineData("group")]
    [InlineData("me")]
    [InlineData("templates")]
    public void Skips_a_file_with_a_newer_version_and_says_it_needs_an_update(string kind)
    {
        var path = Seed(kind, "\"version\": 2,");
        var before = f.Bytes(path);

        f.Load();

        var skipped = Assert.Single(f.Contacts.SkippedFiles);
        Assert.Equal(path, skipped.RelativePath);
        Assert.True(skipped.RequiresUpdate);
        Assert.NotEmpty(skipped.Reason);
        Assert.Equal(before, f.Bytes(path));
    }

    [Theory]
    [InlineData("group")]
    [InlineData("me")]
    [InlineData("templates")]
    public void Loads_a_file_with_version_1(string kind)
    {
        Seed(kind, "\"version\": 1,");

        f.Load();

        AssertLoaded(kind);
        Assert.Empty(f.Contacts.SkippedFiles);
    }
}
