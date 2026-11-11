using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Moving a task to show after one neighbor and before another.</summary>
[Unit]
public sealed class MoveTaskTests
{
    private readonly TasksFixture f = new();

    private static string A => TasksFixture.Id(1);
    private static string B => TasksFixture.Id(2);
    private static string C => TasksFixture.Id(3);
    private static string D => TasksFixture.Id(4);

    public MoveTaskTests()
    {
        f.Seed(A, "A", "a0");
        f.Seed(B, "B", "a1");
        f.Seed(C, "C", "a2");
        f.Seed(D, "D", "a3");
        f.Tasks.UseRepositoryFolder(f.Clone);
    }

    private IReadOnlyList<string> Order => f.Tasks.All.Select(t => t.Title).ToList();

    [Fact]
    public void Moves_a_task_to_the_top()
    {
        f.Tasks.Move(C, null, A);

        Assert.Equal(["C", "A", "B", "D"], Order);
    }

    [Fact]
    public void Moves_a_task_to_the_bottom()
    {
        f.Tasks.Move(B, D, null);

        Assert.Equal(["A", "C", "D", "B"], Order);
    }

    [Fact]
    public void Moves_a_task_between_two_neighbors()
    {
        f.Tasks.Move(D, A, B);

        Assert.Equal(["A", "D", "B", "C"], Order);
    }

    [Fact]
    public void Moves_a_task_down_between_two_neighbors()
    {
        f.Tasks.Move(A, C, D);

        Assert.Equal(["B", "C", "A", "D"], Order);
    }

    [Fact]
    public void Returns_the_moved_task_as_saved()
    {
        f.Time.Advance(TimeSpan.FromMinutes(5));

        var moved = f.Tasks.Move(C, null, A);

        Assert.Equal(f.Tasks.All[0], moved);
        Assert.Equal(f.Time.GetUtcNow(), moved.Updated);
    }

    [Fact]
    public void Changes_only_the_moved_tasks_file()
    {
        var others = new[] { A, B, D }.ToDictionary(id => id, id => f.ReadText(f.TaskFile(id)));

        f.Tasks.Move(C, null, A);

        Assert.All(others, pair => Assert.Equal(pair.Value, f.ReadText(f.TaskFile(pair.Key))));
    }

    [Fact]
    public void Keeps_the_new_order_after_a_restart()
    {
        f.Tasks.Move(C, null, A);
        var restarted = f.Restarted();
        restarted.UseRepositoryFolder(f.Clone);

        Assert.Equal(["C", "A", "B", "D"], restarted.All.Select(t => t.Title));
    }

    [Fact]
    public void Changes_nothing_and_raises_no_notice_when_the_task_already_shows_there()
    {
        var before = f.ReadText(f.TaskFile(B));
        f.Time.Advance(TimeSpan.FromMinutes(5));
        var changes = f.CountChanges();

        var moved = f.Tasks.Move(B, A, C);

        Assert.Equal(before, f.ReadText(f.TaskFile(B)));
        Assert.Equal(f.Tasks.All[1], moved);
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Raises_one_notice_for_a_move()
    {
        var changes = f.CountChanges();

        f.Tasks.Move(C, null, A);

        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Refuses_a_task_that_is_not_there()
    {
        Assert.Throws<TaskNotFoundException>(() => f.Tasks.Move(TasksFixture.Id(99), null, A));
    }

    [Fact]
    public void Refuses_a_neighbor_that_is_not_there()
    {
        Assert.Throws<TaskNotFoundException>(() => f.Tasks.Move(C, TasksFixture.Id(99), null));
        Assert.Throws<TaskNotFoundException>(() => f.Tasks.Move(C, null, TasksFixture.Id(99)));
    }

    [Fact]
    public void Refuses_the_task_as_its_own_neighbor()
    {
        Assert.Throws<ArgumentException>(() => f.Tasks.Move(C, C, D));
        Assert.Throws<ArgumentException>(() => f.Tasks.Move(C, B, C));
    }

    [Fact]
    public void Refuses_a_previous_neighbor_that_does_not_show_before_the_next()
    {
        Assert.Throws<ArgumentException>(() => f.Tasks.Move(A, D, B));
    }

    [Fact]
    public void Changes_nothing_when_a_move_is_refused()
    {
        var before = Order;

        Assert.Throws<ArgumentException>(() => f.Tasks.Move(A, D, B));

        Assert.Equal(before, Order);
    }

    // Neighbors that share a rank have no rank between them.

    private (string First, string Second, string Third) ThreeSharingARank()
    {
        var (x, y, z) = (TasksFixture.Id(11), TasksFixture.Id(12), TasksFixture.Id(13));
        f.Seed(x, "X", "a1");
        f.Seed(y, "Y", "a1");
        f.Seed(z, "Z", "a1");
        f.Tasks.UseRepositoryFolder(f.Clone);
        return (x, y, z);
    }

    [Fact]
    public void Refuses_to_move_a_task_between_neighbors_that_share_a_rank()
    {
        var (x, y, _) = ThreeSharingARank();

        Assert.Throws<ArgumentException>(() => f.Tasks.Move(D, x, y));
    }

    [Fact]
    public void Changes_nothing_when_a_move_between_neighbors_sharing_a_rank_is_refused()
    {
        var (x, y, _) = ThreeSharingARank();
        var before = f.Tasks.All;
        var file = f.ReadText(f.TaskFile(D));

        Assert.Throws<ArgumentException>(() => f.Tasks.Move(D, x, y));

        Assert.Equal(before, f.Tasks.All);
        Assert.Equal(file, f.ReadText(f.TaskFile(D)));
    }

    [Fact]
    public void Changes_nothing_when_the_task_already_shows_between_neighbors_that_share_a_rank()
    {
        var (x, _, _) = ThreeSharingARank();
        var shown = f.Tasks.All.Select(t => t.Id).ToList();
        var middle = shown[shown.IndexOf(x) + 1];
        var changes = f.CountChanges();

        // Of the three tasks sharing a rank, the one shown in the middle already shows between the other two.
        f.Tasks.Move(middle, shown[shown.IndexOf(middle) - 1], shown[shown.IndexOf(middle) + 1]);

        Assert.Equal(shown, f.Tasks.All.Select(t => t.Id));
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Changes_nothing_when_a_task_is_moved_with_no_neighbors_among_other_tasks()
    {
        var before = f.Tasks.All.ToList();
        var file = f.ReadText(f.TaskFile(B));
        var changes = f.CountChanges();

        f.Tasks.Move(B, null, null);

        Assert.Equal(["A", "B", "C", "D"], Order);
        Assert.Equal(before.Select(t => t.Rank), f.Tasks.All.Select(t => t.Rank));
        Assert.Equal(file, f.ReadText(f.TaskFile(B)));
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Changes_nothing_when_the_only_task_is_moved_with_no_neighbors()
    {
        var only = TasksFixture.Id(50);
        var alone = new TasksFixture();
        alone.Seed(only, "Only", "a5");
        alone.Tasks.UseRepositoryFolder(alone.Clone);
        var before = alone.ReadText(alone.TaskFile(only));

        alone.Tasks.Move(only, null, null);

        Assert.Equal(before, alone.ReadText(alone.TaskFile(only)));
    }

    [Fact]
    public void Moves_a_resolved_task_like_any_other()
    {
        f.Seed(C, "C", "a2", resolved: "2026-02-02T00:00:00.000Z");
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.Move(C, null, A);

        Assert.Equal("C", Order[0]);
        Assert.NotNull(f.Tasks.All[0].Resolved);
    }
}
