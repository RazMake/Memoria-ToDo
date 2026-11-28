using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Which repository files the contact file rules handle.</summary>
[Unit]
public sealed class FileRulesHoldsTests
{
    private readonly ContactsFixture f = new();

    [Theory]
    [InlineData("autocomplete/contacts/team.json")]
    [InlineData("autocomplete/contacts/ex-team.json")]
    [InlineData("autocomplete/contacts/me.json")]
    [InlineData("autocomplete/contacts/Friends.json")]
    [InlineData("autocomplete/contacts/dataTypes/careerPaths.json")]
    [InlineData("autocomplete/contacts/dataTypes/pronouns.json")]
    [InlineData("autocomplete/contacts/dataTypes/mentionTemplates.json")]
    public void Holds_contact_files(string path)
    {
        Assert.True(f.Rules.Holds(path));
    }

    [Theory]
    [InlineData("autocomplete/contacts/dataTypes/other.json")]
    [InlineData("autocomplete/contacts/dataTypes/pronouns.txt")]
    [InlineData("autocomplete/contacts/dataTypes/sub/pronouns.json")]
    [InlineData("autocomplete/contacts/sub/team.json")]
    [InlineData("autocomplete/contacts/team.txt")]
    [InlineData("autocomplete/contacts/team.JSON")]
    [InlineData("autocomplete/contacts")]
    [InlineData("autocomplete/team.json")]
    [InlineData("tasks/todo.json")]
    [InlineData("tasks/autocomplete/contacts/team.json")]
    [InlineData("README.md")]
    public void Does_not_hold_other_files(string path)
    {
        Assert.False(f.Rules.Holds(path));
    }

    [Theory]
    [InlineData("/autocomplete/contacts/team.json")]
    [InlineData("C:/autocomplete/contacts/team.json")]
    [InlineData("./autocomplete/contacts/team.json")]
    [InlineData("autocomplete/./contacts/team.json")]
    [InlineData("autocomplete/../autocomplete/contacts/team.json")]
    [InlineData("..")]
    public void Rejects_a_rooted_or_dotted_path(string path)
    {
        Assert.Throws<ArgumentException>(() => f.Rules.Holds(path));
        Assert.Throws<ArgumentException>(() => f.Rules.Merge(path, null, null, null));
    }

    [Theory]
    [InlineData("tasks/todo.json")]
    [InlineData("autocomplete/contacts/dataTypes/other.json")]
    [InlineData("autocomplete/contacts/sub/team.json")]
    public void Refuses_to_merge_a_file_it_does_not_hold(string path)
    {
        Assert.Throws<ArgumentException>(() => f.Rules.Merge(path, null, [1], [2]));
    }
}
