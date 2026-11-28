using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Moves that can't happen change and write nothing.</summary>
[Unit]
public sealed class MoveGroupFailureTests
{
    private readonly ContactsFixture f = new();

    public MoveGroupFailureTests()
    {
        foreach (var (name, position) in new[] { ("a", 1000), ("b", 2000), ("c", 3000), ("d", 4000) })
            f.SeedGroup(name, position, ContactFiles.Person(name + "-1", name));
        f.Load();
        f.ForgetWrites();
    }

    private void AssertRejected(string group, string? previous, string? next)
    {
        var before = f.Snapshot();
        var notices = f.Notices.Count;

        Assert.Throws<ArgumentException>(() => f.Contacts.MoveGroup(group, previous, next));

        f.AssertNothingWritten();
        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }

    [Fact]
    public void Rejects_an_unknown_group() => AssertRejected("nobody", "a", "b");

    [Fact]
    public void Rejects_an_unknown_previous_group() => AssertRejected("d", "nobody", "b");

    [Fact]
    public void Rejects_an_unknown_next_group() => AssertRejected("d", "a", "nobody");

    [Fact]
    public void Rejects_the_group_as_its_own_previous_neighbor() => AssertRejected("b", "b", "c");

    [Fact]
    public void Rejects_the_group_as_its_own_next_neighbor() => AssertRejected("b", "a", "b");

    [Fact]
    public void Rejects_neighbors_with_another_group_between_them() => AssertRejected("d", "a", "c");

    [Fact]
    public void Rejects_neighbors_in_the_wrong_order() => AssertRejected("d", "b", "a");

    [Fact]
    public void Rejects_no_previous_group_when_the_next_one_is_not_the_first() => AssertRejected("d", null, "b");

    [Fact]
    public void Rejects_no_next_group_when_the_previous_one_is_not_the_last() => AssertRejected("a", "b", null);

    [Fact]
    public void Rejects_no_neighbors_at_all_while_other_groups_exist() => AssertRejected("d", null, null);

    [Fact]
    public void Accepts_neighbors_that_are_adjacent_only_once_the_group_is_taken_out()
    {
        f.Contacts.MoveGroup("b", "a", "c");

        Assert.Equal(["a", "b", "c", "d"], f.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public void Needs_a_clone()
    {
        f.Repository.Select(null);

        Assert.Throws<NoRepositoryFolderException>(() => f.Contacts.MoveGroup("d", "a", "b"));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Rejects_a_move_made_while_this_thread_holds_the_repository_lock_before_writing()
    {
        Assert.Throws<InvalidOperationException>(() => f.Repository.Locked(_ => { f.Contacts.MoveGroup("d", "a", "b"); }));

        f.AssertNothingWritten();
        Assert.Equal(["a", "b", "c", "d"], f.Contacts.Groups.Select(group => group.Name));
    }

    [Fact]
    public void Passes_on_the_file_system_s_exception_and_changes_nothing_when_the_write_fails()
    {
        var before = f.Snapshot();
        var notices = f.Notices.Count;
        f.Disk.FailWritesUnder(f.PathOf(ContactsFixture.Folder));

        Assert.Throws<IOException>(() => f.Contacts.MoveGroup("d", "a", "b"));

        Assert.Equal(before, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }
}
