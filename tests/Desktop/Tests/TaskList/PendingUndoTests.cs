using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The one thing the Undo bar can undo at a time: it ends when its ten seconds are up, when another one replaces it, when
/// it is committed, or when it is undone.
/// </summary>
[Unit]
public class PendingUndoTests
{
    private static readonly TimeSpan JustBeforeTheEnd = TimeSpan.FromSeconds(9.9);
    private static readonly TimeSpan TheEnd = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _events = [];
    private readonly Queue<Action> _posted = new();
    private bool _holdPosts;
    private readonly PendingUndo _pending;
    private int _changed;

    public PendingUndoTests()
    {
        _pending = new PendingUndo(_time, Post, () => _changed++);
    }

    private void Post(Action action)
    {
        if (_holdPosts)
        {
            _posted.Enqueue(action);
        }
        else
        {
            action();
        }
    }

    private void Start(string label = "Task deleted", string taskId = "a") =>
        _pending.Start(label, taskId, () => _events.Add($"commit {taskId}"), () => _events.Add($"undo {taskId}"));

    [Fact]
    public void Has_nothing_to_undo_before_one_starts()
    {
        Assert.False(_pending.IsPending);
        Assert.Equal("", _pending.Label);
    }

    [Fact]
    public void Starting_one_makes_it_pending_with_its_label_and_tells_the_bar_to_change()
    {
        Start("Task deleted");

        Assert.True(_pending.IsPending);
        Assert.Equal("Task deleted", _pending.Label);
        Assert.True(_changed > 0);
        Assert.Empty(_events);
    }

    [Fact]
    public void Commits_when_its_ten_seconds_are_up_and_not_before()
    {
        Start();

        _time.Advance(JustBeforeTheEnd);
        Assert.True(_pending.IsPending);
        Assert.Empty(_events);

        _time.Advance(TimeSpan.FromSeconds(0.1));

        Assert.False(_pending.IsPending);
        Assert.Equal(["commit a"], _events);
    }

    [Fact]
    public void Undo_brings_it_back_without_committing_it_and_its_time_running_out_later_does_nothing()
    {
        Start();

        _pending.Undo();
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.False(_pending.IsPending);
        Assert.Equal(["undo a"], _events);
    }

    [Fact]
    public void Undo_tells_the_bar_to_change_before_the_undo_action_runs()
    {
        var pendingWhenUndone = true;
        var changedWhenUndone = 0;
        _pending.Start("x", "a", () => { }, () =>
        {
            pendingWhenUndone = _pending.IsPending;
            changedWhenUndone = _changed;
        });
        var changedByStart = _changed;

        _pending.Undo();

        Assert.False(pendingWhenUndone);
        Assert.True(changedWhenUndone > changedByStart);
    }

    [Fact]
    public void Undo_with_nothing_pending_does_nothing()
    {
        _pending.Undo();

        Assert.Empty(_events);
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void Starting_another_commits_the_pending_one_at_once()
    {
        Start(taskId: "a");

        Start("Edit discarded", taskId: "b");

        Assert.Equal(["commit a"], _events);
        Assert.True(_pending.IsPending);
        Assert.Equal("Edit discarded", _pending.Label);
    }

    [Fact]
    public void The_replaced_ones_time_running_out_does_not_commit_the_one_that_replaced_it()
    {
        Start(taskId: "a");
        _time.Advance(TimeSpan.FromSeconds(6));
        Start(taskId: "b");

        _time.Advance(TimeSpan.FromSeconds(6));

        Assert.True(_pending.IsPending);
        Assert.Equal(["commit a"], _events);

        _time.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(["commit a", "commit b"], _events);
    }

    [Fact]
    public void Commit_finishes_it_for_good_and_its_time_running_out_later_commits_nothing_more()
    {
        Start();

        _pending.Commit();
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.False(_pending.IsPending);
        Assert.Equal(["commit a"], _events);
    }

    [Fact]
    public void Commit_with_nothing_pending_does_nothing()
    {
        _pending.Commit();

        Assert.Empty(_events);
    }

    [Fact]
    public void CommitIfFor_commits_the_pending_one_about_that_task()
    {
        Start(taskId: "a");

        _pending.CommitIfFor("a");

        Assert.Equal(["commit a"], _events);
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void CommitIfFor_leaves_a_pending_one_about_another_task()
    {
        Start(taskId: "a");

        _pending.CommitIfFor("b");

        Assert.Empty(_events);
        Assert.True(_pending.IsPending);
    }

    [Fact]
    public void Commits_on_the_poster_the_window_gave_it_not_on_the_timers_thread()
    {
        Start();
        _holdPosts = true;

        _time.Advance(TheEnd);

        Assert.Empty(_events);
        Assert.True(_pending.IsPending);

        _posted.Dequeue()();

        Assert.Equal(["commit a"], _events);
    }

    [Fact]
    public void A_commit_posted_before_the_user_undid_it_does_nothing_when_it_arrives()
    {
        Start();
        _holdPosts = true;
        _time.Advance(TheEnd);

        _pending.Undo();
        _posted.Dequeue()();

        Assert.Equal(["undo a"], _events);
    }

    [Fact]
    public void A_commit_posted_for_a_replaced_one_does_not_commit_the_one_that_replaced_it()
    {
        Start(taskId: "a");
        _holdPosts = true;
        _time.Advance(TheEnd);

        Start(taskId: "b");
        _posted.Dequeue()();

        Assert.True(_pending.IsPending);
        Assert.DoesNotContain("commit b", _events);
    }

    [Fact]
    public void Disposing_it_stops_the_timer_without_committing()
    {
        Start();

        _pending.Dispose();
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Empty(_events);
        Assert.Equal(0, _time.ActiveTimerCount);
    }
}
