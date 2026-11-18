using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>The groups: their order, headings, kind, and members.</summary>
[Unit]
public sealed class GroupListTests
{
    private readonly ContactsFixture f = new();

    [Fact]
    public void Lists_groups_by_ascending_position()
    {
        f.SeedGroup("c", 3000);
        f.SeedGroup("a", 1000);
        f.SeedGroup("b", 2000);

        f.Load();

        Assert.Equal(["a", "b", "c"], f.Contacts.Groups.Select(group => group.Name));
        Assert.Equal([1000, 2000, 3000], f.Contacts.Groups.Select(group => group.Position));
    }

    [Fact]
    public void Orders_negative_positions_before_positive_ones()
    {
        f.SeedGroup("a", 5);
        f.SeedGroup("b", -5);

        f.Load();

        Assert.Equal(["b", "a"], f.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public void Sorts_groups_with_equal_positions_by_name_ignoring_case()
    {
        f.SeedGroup("beta", 1000);
        f.SeedGroup("Alpha", 1000);
        f.SeedGroup("gamma", 1000);

        f.Load();

        Assert.Equal(["Alpha", "beta", "gamma"], f.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public void Keeps_files_whose_names_differ_only_in_case_as_separate_groups_that_are_both_team_groups()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.SeedGroup("Team", 2000, ContactFiles.Person("2", "Bo"));

        f.Load();

        Assert.Equal(["team", "Team"], f.Contacts.Groups.Select(group => group.Name));
        Assert.All(f.Contacts.Groups, group => Assert.True(group.IsTeam));
        Assert.Equal(["1"], ContactsFixture.Ids(f.Group("team").Members));
        Assert.Equal(["2"], ContactsFixture.Ids(f.Group("Team").Members));
    }

    [Fact]
    public void Sorts_groups_with_equal_positions_by_name_ignoring_case_then_ordinally()
    {
        f.SeedGroup("team", 1000);
        f.SeedGroup("Team", 1000);
        f.SeedGroup("ana", 1000);

        f.Load();

        Assert.Equal(["ana", "Team", "team"], f.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public void Puts_me_among_the_groups_by_its_position()
    {
        f.SeedGroup("team", 1000);
        f.SeedGroup("friends", 50);
        f.SeedMe(100, ContactFiles.Person("1", "Me"));

        f.Load();

        Assert.Equal(["friends", "me", "team"], f.Contacts.Groups.Select(group => group.Name));
    }

    [Theory]
    [InlineData("team", "Team")]
    [InlineData("ex-team", "Ex-Team")]
    [InlineData("me", "Me")]
    [InlineData("old-school-friends", "Old-School-Friends")]
    [InlineData("friends", "Friends")]
    public void Shows_the_file_name_with_each_hyphen_separated_word_capitalized_as_the_heading(string name, string heading)
    {
        f.SeedGroup(name == "me" ? "other" : name, 1000);
        f.SeedMe(100, ContactFiles.Person("1", "Me"));

        f.Load();

        Assert.Equal(heading, f.Group(name).Heading);
    }

    [Fact]
    public void Sorts_the_members_by_full_name_then_ID_ignoring_case()
    {
        f.SeedGroup("team", 1000,
            ContactFiles.Person("9", "zoe"),
            ContactFiles.Person("5", "bob"),
            ContactFiles.Person("4", "Bob"),
            ContactFiles.Person("3", "Ann"));

        f.Load();

        Assert.Equal(["3", "4", "5", "9"], ContactsFixture.Ids(f.Group("team").Members));
    }

    [Theory]
    [InlineData("team", true)]
    [InlineData("ex-team", true)]
    [InlineData("Team", true)]
    [InlineData("EX-TEAM", true)]
    [InlineData("friends", false)]
    [InlineData("teams", false)]
    [InlineData("my-team", false)]
    public void Tells_which_groups_are_team_groups_ignoring_case(string name, bool isTeam)
    {
        f.SeedGroup(name, 1000);

        f.Load();

        Assert.Equal(isTeam, f.Group(name).IsTeam);
    }

    [Fact]
    public void Does_not_count_me_as_a_team_group()
    {
        f.SeedMe(100, ContactFiles.Person("1", "Me"));

        f.Load();

        Assert.False(f.Group("me").IsTeam);
    }

    [Fact]
    public void Leaves_a_skipped_group_file_out_of_the_groups()
    {
        f.SeedGroup("team", 1000);
        f.Seed($"{ContactsFixture.Folder}/broken.json", "{ not json");

        f.Load();

        Assert.Equal(["team"], f.Contacts.Groups.Select(group => group.Name));
    }
}
