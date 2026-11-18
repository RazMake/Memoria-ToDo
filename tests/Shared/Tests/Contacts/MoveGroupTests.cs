using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Moving a group changes positions: one file when a whole number fits between the neighbors, else every loaded group is renumbered.</summary>
[Unit]
public sealed class MoveGroupTests
{
    private readonly ContactsFixture f = new();

    private static string FileOf(string group) => $"{ContactsFixture.Folder}/{group}.json";

    private void Load(params (string Name, int Position)[] groups)
    {
        foreach (var (name, position) in groups)
            f.SeedGroup(name, position, ContactFiles.Person(name + "-1", name.ToUpperInvariant()));
        f.Load();
        f.ForgetWrites();
    }

    private List<int> Positions => [.. f.Contacts.Groups.Select(group => group.Position)];

    private List<string> Names => [.. f.Contacts.Groups.Select(group => group.Name)];

    [Fact]
    public void Puts_the_group_halfway_between_its_new_neighbors_and_writes_only_its_file()
    {
        Load(("a", 1000), ("b", 2000), ("c", 5000));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(["a", "c", "b"], Names);
        Assert.Equal([1000, 1500, 2000], Positions);
        Assert.Equal([FileOf("c")], f.Written);
        Assert.Equal(1500, f.Parsed(FileOf("c")).Position);
    }

    [Fact]
    public void Rounds_a_half_down()
    {
        Load(("a", 1000), ("b", 2001), ("c", 5000));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(1500, f.Group("c").Position);
    }

    [Fact]
    public void Rounds_a_negative_half_toward_minus_infinity()
    {
        Load(("a", -3), ("b", 0), ("c", 5000));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(-2, f.Group("c").Position);
    }

    [Fact]
    public void Works_out_the_halfway_without_overflowing_large_positions()
    {
        Load(("a", int.MaxValue - 10), ("b", int.MaxValue - 2), ("c", 0));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(int.MaxValue - 6, f.Group("c").Position);
    }

    [Fact]
    public void Moves_a_group_that_was_first_to_the_middle_using_the_neighbors_it_has_now()
    {
        Load(("a", 1000), ("b", 2000), ("c", 3000));

        f.Contacts.MoveGroup("a", "b", "c");

        Assert.Equal(["b", "a", "c"], Names);
        Assert.Equal([2000, 2500, 3000], Positions);
    }

    [Fact]
    public void Puts_a_group_at_the_top_1000_before_the_first()
    {
        Load(("a", 1000), ("b", 2000), ("c", 3000));

        f.Contacts.MoveGroup("c", null, "a");

        Assert.Equal(["c", "a", "b"], Names);
        Assert.Equal(0, f.Group("c").Position);
        Assert.Equal([FileOf("c")], f.Written);
    }

    [Fact]
    public void Puts_a_group_at_the_bottom_1000_after_the_last()
    {
        Load(("a", 1000), ("b", 2000), ("c", 3000));

        f.Contacts.MoveGroup("a", "c", null);

        Assert.Equal(["b", "c", "a"], Names);
        Assert.Equal(4000, f.Group("a").Position);
        Assert.Equal([FileOf("a")], f.Written);
    }

    [Fact]
    public void Counts_the_first_position_without_the_moved_group_when_it_goes_to_the_top()
    {
        Load(("a", 1000), ("b", 2000), ("c", 3000));

        f.Contacts.MoveGroup("a", null, "b");

        Assert.Equal(1000, f.Group("a").Position);
        Assert.Equal(["a", "b", "c"], Names);
    }

    [Fact]
    public void Renumbers_every_loaded_group_when_no_whole_number_lies_between_the_neighbors()
    {
        Load(("a", 1000), ("b", 1001), ("c", 5000));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(["a", "c", "b"], Names);
        Assert.Equal([1000, 2000, 3000], Positions);
    }

    [Fact]
    public void Writes_only_the_files_whose_position_changed_when_renumbering()
    {
        Load(("a", 1000), ("b", 1001), ("c", 5000));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal([FileOf("b"), FileOf("c")], f.Written);
        Assert.Equal(2000, f.Parsed(FileOf("c")).Position);
        Assert.Equal(3000, f.Parsed(FileOf("b")).Position);
    }

    [Fact]
    public void Renumbers_when_the_neighbors_have_equal_positions()
    {
        Load(("a", 1000), ("b", 1000), ("c", 5000));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(["a", "c", "b"], Names);
        Assert.Equal([1000, 2000, 3000], Positions);
    }

    [Fact]
    public void Renumbers_the_user_s_own_group_with_the_others()
    {
        f.SeedMe(100, ContactFiles.Person("42", "Me"));
        Load(("a", 1000), ("b", 1001), ("c", 5000));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(["me", "a", "c", "b"], Names);
        Assert.Equal([1000, 2000, 3000, 4000], Positions);
        Assert.Contains(ContactsFixture.MeFile, f.Written);
    }

    [Fact]
    public void Lets_a_skipped_group_file_take_no_part_in_renumbering()
    {
        f.Seed(FileOf("broken"), "{ not json");
        Load(("a", 1000), ("b", 1001), ("c", 5000));

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal([1000, 2000, 3000], Positions);
        Assert.DoesNotContain(FileOf("broken"), f.Written);
        Assert.Equal("{ not json", f.Text(FileOf("broken")));
    }

    [Fact]
    public void Changes_only_the_position_and_keeps_members_and_unknown_fields()
    {
        var original =
            "{\n  \"version\": 1,\n  \"position\": 5000,\n  \"members\": [\n    {\n      \"id\": \"1\",\n      \"fullName\": \"Ana\",\n      \"extra\": \"x\"\n    }\n  ],\n  \"note\": 7\n}";
        f.SeedGroup("a", 1000);
        f.SeedGroup("b", 2000);
        f.Seed(FileOf("c"), original);
        f.Load();
        f.ForgetWrites();

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(original.Replace("\"position\": 5000", "\"position\": 1500"), f.Text(FileOf("c")));
        Assert.Equal([new Contact("1", "Ana")], f.Group("c").Members);
    }

    [Fact]
    public void Raises_Changed()
    {
        Load(("a", 1000), ("b", 2000), ("c", 5000));
        var before = f.Notices.Count(notice => notice == "Changed");

        f.Contacts.MoveGroup("c", "a", "b");

        Assert.Equal(before + 1, f.Notices.Count(notice => notice == "Changed"));
    }

    [Fact]
    public void Moves_only_the_group_named_exactly_when_another_differs_only_in_case()
    {
        Load(("team", 1000), ("Team", 2000), ("c", 3000));
        var lower = f.Bytes(FileOf("team"));

        f.Contacts.MoveGroup("Team", null, "team");

        Assert.Equal(["Team", "team", "c"], Names);
        Assert.Equal(0, f.Group("Team").Position);
        Assert.Equal([FileOf("Team")], f.Written);
        Assert.Equal(lower, f.Bytes(FileOf("team")));
    }

    [Fact]
    public void Moves_the_user_s_own_group_like_any_other()
    {
        f.SeedMe(100, ContactFiles.Person("42", "Me"));
        Load(("a", 1000), ("b", 2000));

        f.Contacts.MoveGroup("me", "b", null);

        Assert.Equal(["a", "b", "me"], Names);
        Assert.Equal(3000, f.Group("me").Position);
    }

    [Fact]
    public void Renumbers_every_group_1000_2000_and_so_on_when_the_top_position_would_leave_the_int_range()
    {
        Load(("a", int.MinValue + 500), ("b", 0), ("c", 5000));

        f.Contacts.MoveGroup("c", null, "a");

        Assert.Equal(["c", "a", "b"], Names);
        Assert.Equal([1000, 2000, 3000], Positions);
        Assert.Equal([FileOf("a"), FileOf("b"), FileOf("c")], f.Written);
    }

    [Fact]
    public void Renumbers_every_group_1000_2000_and_so_on_when_the_bottom_position_would_leave_the_int_range()
    {
        Load(("a", 0), ("b", 5000), ("c", int.MaxValue - 500));

        f.Contacts.MoveGroup("a", "c", null);

        Assert.Equal(["b", "c", "a"], Names);
        Assert.Equal([1000, 2000, 3000], Positions);
        Assert.Equal([FileOf("a"), FileOf("b"), FileOf("c")], f.Written);
    }

    [Theory]
    [InlineData("a", null, "b")]
    [InlineData("b", "a", "c")]
    [InlineData("c", "b", null)]
    public void Writes_nothing_and_raises_nothing_when_the_group_moves_to_where_it_already_is(string group, string? previous, string? next)
    {
        Load(("a", 1000), ("b", 2000), ("c", 3000));
        var notices = f.Notices.Count;

        f.Contacts.MoveGroup(group, previous, next);

        f.AssertNothingWritten();
        Assert.Equal(["a", "b", "c"], Names);
        Assert.Equal([1000, 2000, 3000], Positions);
        Assert.Equal(notices, f.Notices.Count);
    }

    // A renumber of a, b and c that fails on the second file written, whichever file that is.
    private void LoadRenumberThatFailsOnTheSecondFile(Exception failure)
    {
        Load(("a", 1000), ("b", 1001), ("c", 5000));
        f.Files.FailWritesAfter(1, failure);
    }

    [Fact]
    public void Publishes_what_a_renumber_wrote_and_throws_the_write_s_exception_as_it_is_when_a_later_write_fails()
    {
        var failure = new IOException("disk full");
        LoadRenumberThatFailsOnTheSecondFile(failure);
        var before = f.Notices.Count;

        var thrown = Assert.Throws<IOException>(() => f.Contacts.MoveGroup("c", "a", "b"));

        Assert.Same(failure, thrown);
        Assert.Equal(["Changed", "LocalSaved"], f.Notices.Skip(before));
        var written = Assert.Single(f.Written);
        var original = new Dictionary<string, int> { ["a"] = 1000, ["b"] = 1001, ["c"] = 5000 };
        var renumbered = new Dictionary<string, int> { ["a"] = 1000, ["c"] = 2000, ["b"] = 3000 };
        foreach (var group in f.Contacts.Groups)
        {
            var expected = (written == FileOf(group.Name) ? renumbered : original)[group.Name];
            Assert.Equal(expected, group.Position);
            Assert.Equal(expected, f.Parsed(FileOf(group.Name)).Position);
        }
    }

    [Fact]
    public void Throws_an_AggregateException_with_the_write_s_failure_then_the_handler_s_when_both_fail()
    {
        var write = new IOException("disk full");
        var handler = new InvalidOperationException("handler");
        LoadRenumberThatFailsOnTheSecondFile(write);
        f.Contacts.Changed += (_, _) => throw handler;
        var before = f.Notices.Count;

        var thrown = Assert.Throws<AggregateException>(() => f.Contacts.MoveGroup("c", "a", "b"));

        Assert.Equal(new Exception[] { write, handler }, thrown.InnerExceptions);
        Assert.Equal(["Changed", "LocalSaved"], f.Notices.Skip(before));
    }

    [Fact]
    public void Throws_an_AggregateException_with_the_write_s_failure_then_Changed_s_then_the_report_s_when_all_fail()
    {
        var write = new IOException("disk full");
        var changed = new InvalidOperationException("handler");
        var report = new InvalidOperationException("report");
        LoadRenumberThatFailsOnTheSecondFile(write);
        f.Contacts.Changed += (_, _) => throw changed;
        f.Repository.LocalSaved += (_, _) => throw report;

        var thrown = Assert.Throws<AggregateException>(() => f.Contacts.MoveGroup("c", "a", "b"));

        Assert.Equal(new Exception[] { write, changed, report }, thrown.InnerExceptions);
    }
}
