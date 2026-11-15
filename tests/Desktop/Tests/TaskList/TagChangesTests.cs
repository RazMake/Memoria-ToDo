using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Adding or removing a tag on the expanded task, with no Edit: each change saves at once through Tasks' save of an edit,
/// with the task as the list shows it and only its tags different, then waits on the Undo bar. The tests ask for a change
/// the way the window does, with the task as shown, and see what Tasks keeps, what the Undo bar says, and what is reported.
/// </summary>
[Unit]
public class TagChangesTests
{
    private const string Added = "Tag added";
    private const string Removed = "Tag removed";
    private const string ImagesMessage = "The edit's images couldn't be removed: ";
    private static readonly TimeSpan BarEnds = TimeSpan.FromSeconds(10);

    private readonly ListTasks _tasks = new();
    private readonly PendingUndo _pending;
    private readonly EditCleanup _cleanup;
    private readonly List<string> _reports = [];
    private readonly TagChanges _changes;

    public TagChangesTests()
    {
        _pending = new PendingUndo(_tasks.Time, post => post(), () => { });
        _cleanup = new EditCleanup(_tasks.Tasks, _pending, _reports.Add);
        _changes = new TagChanges(_tasks.Tasks, _pending, _cleanup, _reports.Add);
    }

    private TaskItem Stored(TaskItem task) => _tasks.Tasks.All.Single(t => t.Id == task.Id);

    private IReadOnlyList<string> StoredTags(TaskItem task) => Stored(task).Tags;

    // Another device saves a new title for the task, which reaches this one while the list still shows the old task.
    private void RetitleElsewhere(TaskItem shown, string title) =>
        _tasks.Tasks.SaveEdit(shown, new TaskDraft(shown.Id, title, shown.Description, shown.Tags));

    private void RetagElsewhere(TaskItem shown, params string[] tags) =>
        _tasks.Tasks.SaveEdit(shown, new TaskDraft(shown.Id, shown.Title, shown.Description, tags));

    private TaskItem ChangeBy(bool add, TaskItem shown, string tag) =>
        (add ? _changes.Add(shown, tag) : _changes.Remove(shown, tag))!;

    // ---- The save ----

    [Fact]
    public void Adding_a_tag_saves_the_task_with_the_tag_at_the_end_and_nothing_else_changed()
    {
        var shown = _tasks.Add("Buy milk", "Oat milk", "home");

        var saved = _changes.Add(shown, "errand");

        Assert.NotNull(saved);
        Assert.Equal(["home", "errand"], saved.Tags);
        Assert.Equal(saved, Stored(shown));
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Oat milk", saved.Description);
        Assert.Equal(shown.Rank, saved.Rank);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Removing_a_tag_saves_the_task_without_it_ignoring_case_and_keeps_the_other_tags_in_order()
    {
        var shown = _tasks.Add("Buy milk", "Oat milk", "Home", "errand", "milk");

        var saved = _changes.Remove(shown, "home");

        Assert.NotNull(saved);
        Assert.Equal(["errand", "milk"], saved.Tags);
        Assert.Equal(saved, Stored(shown));
        Assert.Equal("Oat milk", saved.Description);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_title_changed_on_another_device_meanwhile_survives_the_tag_change(bool add)
    {
        var shown = _tasks.Add("Buy milk", "", "home", "errand");
        RetitleElsewhere(shown, "Buy oat milk");

        ChangeBy(add, shown, add ? "milk" : "errand");

        var stored = Stored(shown);
        Assert.Equal("Buy oat milk", stored.Title);
        string[] expected = add ? ["home", "errand", "milk"] : ["home"];
        Assert.Equal(expected, stored.Tags);
    }

    [Fact]
    public void A_tag_another_device_added_meanwhile_stays_when_a_tag_is_added()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        RetagElsewhere(shown, "home", "urgent");

        _changes.Add(shown, "errand");

        Assert.Equivalent(new[] { "home", "urgent", "errand" }, StoredTags(shown));
    }

    // ---- The Undo bar ----

    [Fact]
    public void Adding_a_tag_shows_the_Tag_added_bar_for_the_same_time_as_a_delete()
    {
        var shown = _tasks.Add("Buy milk");

        _changes.Add(shown, "home");

        Assert.True(_pending.IsPending);
        Assert.Equal(Added, _pending.Label);
        _tasks.Time.Advance(BarEnds - TimeSpan.FromSeconds(1));
        Assert.True(_pending.IsPending);
        _tasks.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void Removing_a_tag_shows_the_Tag_removed_bar()
    {
        var shown = _tasks.Add("Buy milk", "", "home");

        _changes.Remove(shown, "home");

        Assert.True(_pending.IsPending);
        Assert.Equal(Removed, _pending.Label);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_change_stays_when_the_bar_ends(bool add)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var saved = ChangeBy(add, shown, add ? "errand" : "home");
        string[] expected = add ? ["home", "errand"] : [];

        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(expected, saved.Tags);
        Assert.Equal(expected, StoredTags(shown));
        Assert.Empty(_reports);
    }

    [Fact]
    public void Undo_of_an_added_tag_removes_it()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _changes.Add(shown, "errand");

        _pending.Undo();

        Assert.Equal(["home"], StoredTags(shown));
        Assert.False(_pending.IsPending);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Undo_of_a_removed_tag_puts_it_back_at_the_end()
    {
        var shown = _tasks.Add("Buy milk", "", "home", "errand", "milk");
        _changes.Remove(shown, "home");

        _pending.Undo();

        Assert.Equal(["errand", "milk", "home"], StoredTags(shown));
        Assert.False(_pending.IsPending);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Undo_changes_only_that_tag_so_a_title_changed_elsewhere_meanwhile_stays(bool add)
    {
        var shown = _tasks.Add("Buy milk", "", "home", "errand");
        var saved = ChangeBy(add, shown, add ? "milk" : "errand");
        RetitleElsewhere(saved, "Buy oat milk");

        _pending.Undo();

        var stored = Stored(shown);
        Assert.Equal("Buy oat milk", stored.Title);
        Assert.Equal(["home", "errand"], stored.Tags);
    }

    [Fact]
    public void Undo_does_nothing_more_once_the_bar_is_gone_and_leaves_no_second_save()
    {
        var shown = _tasks.Add("Buy milk");
        _changes.Add(shown, "home");
        _pending.Undo();
        var after = Stored(shown);

        _pending.Undo();
        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(after, Stored(shown));
    }

    // ---- One bar at a time ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_new_tag_change_finishes_the_earlier_bar_keeps_its_change_and_Undo_undoes_only_the_new_one(bool sameTask)
    {
        var first = _tasks.Add("First");
        var other = sameTask ? first : _tasks.Add("Other");
        _changes.Add(first, "one");

        var saved = _changes.Add(sameTask ? Stored(first) : other, "two");
        Assert.NotNull(saved);
        _pending.Undo();

        Assert.Equal(["one"], StoredTags(first));
        string[] otherTags = sameTask ? ["one"] : [];
        Assert.Equal(otherTags, StoredTags(other));
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void Deleting_a_task_finishes_the_tag_bar_and_keeps_the_change()
    {
        var shown = _tasks.Add("Buy milk");
        var doomed = _tasks.Add("Call Ana");
        _changes.Add(shown, "home");
        var deletion = new TaskDeletion(_tasks.Tasks, new ListContent(_tasks.Tasks, new ListFilter()), _pending, _reports.Add, () => { });

        deletion.Start(doomed.Id);
        _pending.Undo();

        Assert.Equal(["home"], StoredTags(shown));
        Assert.Single(_tasks.Tasks.All, task => task.Id == doomed.Id);
    }

    [Fact]
    public void A_discarded_edit_finishes_the_tag_bar_and_keeps_the_change()
    {
        var shown = _tasks.Add("Buy milk");
        var edited = _tasks.Add("Call Ana");
        _changes.Add(shown, "home");

        _cleanup.Leave(edited.Id, hasChanges: true, () => { });

        Assert.Equal("Edit discarded", _pending.Label);
        Assert.Equal(["home"], StoredTags(shown));
    }

    [Fact]
    public void Starting_an_edit_of_the_task_finishes_its_tag_bar_and_keeps_the_change()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _changes.Remove(shown, "home");

        var started = _cleanup.BeginFresh(shown.Id);
        _pending.Undo();

        Assert.True(started);
        Assert.False(_pending.IsPending);
        Assert.Empty(StoredTags(shown));
    }

    [Fact]
    public void Starting_an_edit_of_another_task_leaves_the_tag_bar()
    {
        var shown = _tasks.Add("Buy milk");
        var other = _tasks.Add("Call Ana");
        _changes.Add(shown, "home");

        _cleanup.BeginFresh(other.Id);

        Assert.True(_pending.IsPending);
        Assert.Equal(Added, _pending.Label);
        _pending.Undo();
        Assert.Empty(StoredTags(shown));
    }

    // ---- Cleaning up the task's earlier edit first ----

    [Fact]
    public void A_tag_change_first_finishes_the_tasks_discarded_edit_bar_and_deletes_its_images()
    {
        var shown = _tasks.Add("Buy milk");
        var images = _tasks.PasteImage(shown.Id);
        var restored = 0;
        _cleanup.Leave(shown.Id, hasChanges: true, () => restored++);

        var saved = _changes.Add(shown, "home");

        Assert.NotNull(saved);
        Assert.Equal(Added, _pending.Label);
        Assert.DoesNotContain(images, _tasks.Files.FileExists);
        Assert.Equal(0, restored);
        Assert.Empty(_reports);
    }

    [Fact]
    public void A_tag_change_deletes_the_images_an_earlier_edit_left_so_saving_does_not_move_them_into_the_clone()
    {
        var shown = _tasks.Add("Buy milk");
        var images = _tasks.PasteImage(shown.Id);

        _changes.Add(shown, "home");

        Assert.DoesNotContain(images, _tasks.Files.FileExists);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_the_leftover_images_cannot_be_deleted_nothing_is_saved_and_the_message_says_why(bool add)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _tasks.StopDeleting(_tasks.PasteImage(shown.Id));

        var saved = add ? _changes.Add(shown, "errand") : _changes.Remove(shown, "home");

        Assert.Null(saved);
        Assert.Equal(shown, Stored(shown));
        Assert.False(_pending.IsPending);
        Assert.NotEmpty(_reports);
        Assert.All(_reports, report => Assert.StartsWith(ImagesMessage, report));
        Assert.Contains("Deleting is not allowed here", _reports[^1]);
    }

    // ---- Failures ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_failed_save_changes_nothing_shows_no_bar_and_says_why(bool add)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var saved = add ? _changes.Add(shown, "errand") : _changes.Remove(shown, "home");

        Assert.Null(saved);
        Assert.Equal(["home"], StoredTags(shown));
        Assert.False(_pending.IsPending);
        Assert.Contains("Writing is not allowed here", Assert.Single(_reports));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_task_deleted_meanwhile_says_it_no_longer_exists_and_shows_no_bar(bool add)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _tasks.Tasks.Delete(shown.Id);

        var saved = add ? _changes.Add(shown, "errand") : _changes.Remove(shown, "home");

        Assert.Null(saved);
        Assert.Equal("This task no longer exists.", Assert.Single(_reports));
        Assert.False(_pending.IsPending);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_failed_Undo_keeps_the_change_removes_the_bar_and_says_why(bool add)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var saved = ChangeBy(add, shown, add ? "errand" : "home");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        _pending.Undo();
        _tasks.Files.StopFailing();
        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(saved.Tags, StoredTags(shown));
        Assert.False(_pending.IsPending);
        Assert.Contains("Writing is not allowed here", Assert.Single(_reports));
    }

    [Fact]
    public void Undo_of_a_change_on_a_task_deleted_meanwhile_says_it_no_longer_exists()
    {
        var shown = _tasks.Add("Buy milk");
        _changes.Add(shown, "home");
        _tasks.Tasks.Delete(shown.Id);

        _pending.Undo();

        Assert.Equal("This task no longer exists.", Assert.Single(_reports));
        Assert.False(_pending.IsPending);
    }
}
