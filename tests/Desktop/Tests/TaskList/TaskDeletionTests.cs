using System.IO;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Deleting a task from the list: it hides at once with no confirmation, waits on the Undo bar, and is deleted for good
/// when the bar ends. A failure comes back with its reason.
/// </summary>
[Unit]
public class TaskDeletionTests
{
    private static readonly TimeSpan BarEnds = TimeSpan.FromSeconds(10);

    private readonly ListTasks _tasks = new();
    private readonly ListContent _content;
    private readonly PendingUndo _pending;
    private readonly List<string> _reports = [];
    private readonly TaskDeletion _deletion;
    private int _changed;

    public TaskDeletionTests()
    {
        _content = new ListContent(_tasks.Tasks, new ListFilter());
        _pending = new PendingUndo(_tasks.Time, post => post(), () => _changed++);
        _deletion = new TaskDeletion(_tasks.Tasks, _content, _pending, _reports.Add, () => _changed++);
    }

    [Fact]
    public void Starting_hides_the_task_at_once_and_shows_the_Undo_bar_without_deleting_it()
    {
        var task = _tasks.Add("Buy milk");
        _tasks.Add("Call Ana");

        _deletion.Start(task.Id);

        Assert.Equal(task.Id, _content.HiddenId);
        Assert.Equal(["Call Ana"], _content.Build(null, null).Open.Select(t => t.Title));
        Assert.True(_pending.IsPending);
        Assert.Equal("Task deleted", _pending.Label);
        Assert.Equal(2, _tasks.Tasks.All.Count);
    }

    [Fact]
    public void Undo_brings_the_task_back_and_it_is_never_deleted()
    {
        var task = _tasks.Add("Buy milk");
        _deletion.Start(task.Id);

        _pending.Undo();
        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.Null(_content.HiddenId);
        Assert.Equal(["Buy milk"], _content.Build(null, null).Open.Select(t => t.Title));
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Deletes_the_task_for_good_when_the_bar_ends_and_not_before()
    {
        var task = _tasks.Add("Buy milk");
        _deletion.Start(task.Id);

        _tasks.Time.Advance(BarEnds - TimeSpan.FromSeconds(1));
        Assert.Single(_tasks.Tasks.All);

        _tasks.Time.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(_tasks.Tasks.All);
        Assert.Null(_content.HiddenId);
        Assert.False(_pending.IsPending);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Deleting_another_task_deletes_the_first_one_at_once_and_keeps_the_second_hidden_on_the_one_bar()
    {
        var a = _tasks.Add("A");
        var b = _tasks.Add("B");
        _deletion.Start(a.Id);

        _deletion.Start(b.Id);

        Assert.DoesNotContain(_tasks.Tasks.All, task => task.Id == a.Id);
        Assert.Equal(b.Id, _content.HiddenId);
        Assert.True(_pending.IsPending);
    }

    [Fact]
    public void Committing_the_bar_as_the_window_closes_deletes_the_task()
    {
        var task = _tasks.Add("Buy milk");
        _deletion.Start(task.Id);

        _pending.Commit();

        Assert.Empty(_tasks.Tasks.All);
    }

    [Fact]
    public void Tells_the_list_to_refresh_when_it_starts_and_when_the_task_is_gone()
    {
        var task = _tasks.Add("Buy milk");
        _deletion.Start(task.Id);
        var afterStart = _changed;

        _tasks.Time.Advance(BarEnds);

        Assert.True(afterStart > 0);
        Assert.True(_changed > afterStart);
    }

    [Fact]
    public void A_task_file_that_cannot_be_deleted_stays_with_the_reason_reported_and_is_shown_again()
    {
        var task = _tasks.Add("Buy milk");
        _deletion.Start(task.Id);
        _tasks.Files.FailDeletesUnder(_tasks.TaskFolder);

        _tasks.Time.Advance(BarEnds);

        Assert.Single(_tasks.Tasks.All);
        Assert.Null(_content.HiddenId);
        Assert.Equal(["Buy milk"], _content.Build(null, null).Open.Select(t => t.Title));
        Assert.Contains("Deleting is not allowed here", Assert.Single(_reports));
    }

    [Fact]
    public void Images_that_cannot_be_removed_leave_the_task_deleted_and_say_so_with_the_reason()
    {
        var task = _tasks.Add("Buy milk");
        var images = Path.Combine(_tasks.Clone, "images");
        _tasks.Files.WriteFile(Path.Combine(images, task.Id, "photo.png"), [1, 2, 3]);
        _deletion.Start(task.Id);
        _tasks.Files.FailDeletesUnder(images);

        _tasks.Time.Advance(BarEnds);

        Assert.Empty(_tasks.Tasks.All);
        Assert.Null(_content.HiddenId);
        var report = Assert.Single(_reports);
        Assert.StartsWith("The task was deleted, but some of its images couldn't be removed: ", report);
        Assert.Contains("Deleting is not allowed here", report);
    }

    [Fact]
    public void A_task_deleted_elsewhere_meanwhile_is_not_an_error()
    {
        var task = _tasks.Add("Buy milk");
        _deletion.Start(task.Id);
        _tasks.Tasks.Delete(task.Id);

        _tasks.Time.Advance(BarEnds);

        Assert.Empty(_reports);
        Assert.Null(_content.HiddenId);
    }
}
