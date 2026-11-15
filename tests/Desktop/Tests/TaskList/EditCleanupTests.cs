using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// An edit's pasted images: they stay while the user can still Undo the discard, and go when the Undo bar goes away,
/// another bar replaces it, a fresh edit of the task starts, or the window closes. They never touch the draft's images.
/// The tests find an image by the files the paste adds to the fake file system, so they don't depend on where Tasks keeps them.
/// </summary>
[Unit]
public class EditCleanupTests
{
    private const string Message = "The edit's images couldn't be removed: ";
    private const string Reason = "Deleting is not allowed here";
    private static readonly TimeSpan BarEnds = TimeSpan.FromSeconds(10);

    private readonly ListTasks _tasks = new();
    private readonly PendingUndo _pending;
    private readonly List<string> _reports = [];
    private readonly EditCleanup _cleanup;
    private int _restored;

    public EditCleanupTests()
    {
        _pending = new PendingUndo(_tasks.Time, post => post(), () => { });
        _cleanup = new EditCleanup(_tasks.Tasks, _pending, _reports.Add);
    }

    private IReadOnlyList<string> PasteImage(string taskId) => _tasks.PasteImage(taskId);

    private bool AnyKept(IEnumerable<string> files) => files.Any(_tasks.Files.FileExists);

    private void StopDeleting(IReadOnlyList<string> images) => _tasks.StopDeleting(images);

    private void Restore() => _restored++;

    [Fact]
    public void Leaving_a_clean_edit_deletes_its_images_at_once_with_no_Undo_bar()
    {
        var task = _tasks.Add("Buy milk");
        var images = PasteImage(task.Id);

        _cleanup.Leave(task.Id, hasChanges: false, Restore);

        Assert.False(AnyKept(images));
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void Leaving_a_changed_edit_keeps_its_images_and_offers_Undo_for_the_discarded_edit()
    {
        var task = _tasks.Add("Buy milk");
        var images = PasteImage(task.Id);

        _cleanup.Leave(task.Id, hasChanges: true, Restore);

        Assert.True(_pending.IsPending);
        Assert.Equal("Edit discarded", _pending.Label);
        Assert.All(images, image => Assert.True(_tasks.Files.FileExists(image)));
    }

    [Fact]
    public void Undo_brings_the_edit_back_and_keeps_its_images_even_when_deleting_would_fail()
    {
        var task = _tasks.Add("Buy milk");
        var images = PasteImage(task.Id);
        StopDeleting(images);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);

        _pending.Undo();
        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, _restored);
        Assert.Empty(_reports);
        Assert.All(images, image => Assert.True(_tasks.Files.FileExists(image)));
    }

    [Fact]
    public void A_discarded_edits_images_are_deleted_when_the_Undo_bar_goes_away_and_the_task_is_unchanged()
    {
        var task = _tasks.Add("Buy milk");
        var images = PasteImage(task.Id);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);

        _tasks.Time.Advance(BarEnds);

        Assert.False(AnyKept(images));
        Assert.Equal(0, _restored);
        Assert.Equal(task, _tasks.Tasks.All.Single());
    }

    [Fact]
    public void A_discarded_edits_images_are_deleted_when_another_Undo_bar_replaces_its_bar()
    {
        var task = _tasks.Add("Buy milk");
        var images = PasteImage(task.Id);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);

        _pending.Start("Task deleted", "other", () => { }, () => { });

        Assert.False(AnyKept(images));
    }

    [Fact]
    public void A_discarded_edits_images_are_deleted_when_the_window_commits_the_bar_as_it_closes()
    {
        var task = _tasks.Add("Buy milk");
        var images = PasteImage(task.Id);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);

        _pending.Commit();

        Assert.False(AnyKept(images));
    }

    [Fact]
    public void A_failure_when_the_Undo_bar_expires_is_reported_with_its_reason_and_the_images_stay()
    {
        var task = _tasks.Add("Buy milk");
        var images = PasteImage(task.Id);
        StopDeleting(images);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);

        _tasks.Time.Advance(BarEnds);

        var report = Assert.Single(_reports);
        Assert.StartsWith(Message, report);
        Assert.Contains(Reason, report);
        Assert.Contains(images, _tasks.Files.FileExists);
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void A_failure_when_another_bar_replaces_the_bar_is_reported_and_the_replacing_one_goes_on()
    {
        var task = _tasks.Add("Buy milk");
        var images = PasteImage(task.Id);
        StopDeleting(images);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);

        _pending.Start("Task deleted", "other", () => { }, () => { });

        Assert.StartsWith(Message, Assert.Single(_reports));
        Assert.True(_pending.IsPending);
        Assert.Equal("Task deleted", _pending.Label);
    }

    [Fact]
    public void Leaving_a_clean_edit_whose_images_cannot_be_removed_reports_why_and_does_not_block_leaving()
    {
        var task = _tasks.Add("Buy milk");
        StopDeleting(PasteImage(task.Id));

        _cleanup.Leave(task.Id, hasChanges: false, Restore);

        var report = Assert.Single(_reports);
        Assert.StartsWith(Message, report);
        Assert.Contains(Reason, report);
    }

    [Fact]
    public void Closing_an_edit_still_open_deletes_its_images_and_leaves_the_task_unchanged()
    {
        var task = _tasks.Add("Buy milk", "Remember the oat milk");
        var images = PasteImage(task.Id);

        _cleanup.Close(task.Id);

        Assert.False(AnyKept(images));
        Assert.Equal(task, _tasks.Tasks.All.Single());
    }

    [Fact]
    public void A_failure_closing_an_edit_is_reported_with_its_reason()
    {
        var task = _tasks.Add("Buy milk");
        StopDeleting(PasteImage(task.Id));

        _cleanup.Close(task.Id);

        Assert.StartsWith(Message, Assert.Single(_reports));
    }

    [Fact]
    public void Starting_a_fresh_edit_first_removes_the_images_an_earlier_edit_of_the_task_left()
    {
        var task = _tasks.Add("A");
        var images = PasteImage(task.Id);
        StopDeleting(images);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);
        _tasks.Time.Advance(BarEnds);
        Assert.Contains(images, _tasks.Files.FileExists);
        _tasks.Files.StopFailing();

        var started = _cleanup.BeginFresh(task.Id);

        Assert.True(started);
        Assert.False(AnyKept(images));
    }

    [Fact]
    public void A_fresh_edit_does_not_start_while_the_leftovers_cannot_be_removed_and_says_why()
    {
        var task = _tasks.Add("A");
        var other = _tasks.Add("B");
        StopDeleting(PasteImage(task.Id));
        _cleanup.Leave(task.Id, hasChanges: true, Restore);
        _tasks.Time.Advance(BarEnds);
        _reports.Clear();

        var started = _cleanup.BeginFresh(task.Id);

        Assert.False(started);
        Assert.StartsWith(Message, Assert.Single(_reports));
        Assert.True(_cleanup.BeginFresh(other.Id));
    }

    [Fact]
    public void Starting_a_fresh_edit_of_the_task_commits_its_own_Undo_bar_so_the_earlier_discard_is_final()
    {
        var task = _tasks.Add("A");
        var images = PasteImage(task.Id);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);

        _cleanup.BeginFresh(task.Id);

        Assert.False(_pending.IsPending);
        Assert.False(AnyKept(images));
        Assert.Equal(0, _restored);
    }

    [Fact]
    public void Starting_a_fresh_edit_of_the_task_means_the_earlier_bars_expiry_later_deletes_nothing_of_the_new_edit()
    {
        var task = _tasks.Add("A");
        PasteImage(task.Id);
        _cleanup.Leave(task.Id, hasChanges: true, Restore);
        _cleanup.BeginFresh(task.Id);
        var fresh = PasteImage(task.Id);

        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.All(fresh, image => Assert.True(_tasks.Files.FileExists(image)));
    }

    [Fact]
    public void Starting_a_fresh_edit_of_another_task_leaves_a_pending_bar_about_a_different_task()
    {
        var a = _tasks.Add("A");
        var b = _tasks.Add("B");
        _cleanup.Leave(a.Id, hasChanges: true, Restore);

        _cleanup.BeginFresh(b.Id);

        Assert.True(_pending.IsPending);
    }
}
