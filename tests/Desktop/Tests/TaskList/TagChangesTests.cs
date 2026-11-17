using System.IO;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Choosing or clearing the one tag of the selected task, with no Edit: each change saves at once through Tasks' save of an
/// edit, with the task as the list shows it and only its tag different, then waits on the Undo bar, which saves the tag
/// the pill showed before. The tests ask for a change the way the window does, with the task as shown, and see what Tasks
/// keeps, what the Undo bar says, what is reported, and what happens to the images still waiting for the task.
/// </summary>
[Unit]
public class TagChangesTests
{
    private const string Changed = "Tag changed";
    private const string Cleared = "Tag cleared";
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
        _tasks.Tasks.SaveEdit(shown, new TaskDraft(shown.Id, title, shown.Description, [.. shown.Tags.Take(1)]));

    // Another device chooses another tag, which reaches this one while the list still shows the old task.
    private void RetagElsewhere(TaskItem shown, string tag) =>
        _tasks.Tasks.SaveEdit(shown, new TaskDraft(shown.Id, shown.Title, shown.Description, [tag]));

    // ---- The save ----

    [Fact]
    public void Choosing_a_tag_saves_it_as_the_tasks_only_tag_and_nothing_else_changed()
    {
        var shown = _tasks.Add("Buy milk", "Oat milk");

        var saved = _changes.Choose(shown, "errand");

        Assert.NotNull(saved);
        Assert.Equal(["errand"], saved.Tags);
        Assert.Equal(saved, Stored(shown));
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Oat milk", saved.Description);
        Assert.Equal(shown.Rank, saved.Rank);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Choosing_a_tag_replaces_the_tag_the_task_had()
    {
        var shown = _tasks.Add("Buy milk", "Oat milk", "home");

        var saved = _changes.Choose(shown, "errand");

        Assert.NotNull(saved);
        Assert.Equal(["errand"], saved.Tags);
        Assert.Equal(["errand"], StoredTags(shown));
    }

    [Fact]
    public void Choosing_a_tag_spells_it_as_the_matching_tag_in_use()
    {
        _tasks.Add("Other", "", "Home");
        var shown = _tasks.Add("Buy milk");

        var saved = _changes.Choose(shown, "home");

        Assert.NotNull(saved);
        Assert.Equal(["Home"], saved.Tags);
    }

    [Fact]
    public void Clearing_saves_the_task_with_no_tag_and_nothing_else_changed()
    {
        var shown = _tasks.Add("Buy milk", "Oat milk", "home");

        var saved = _changes.Clear(shown);

        Assert.NotNull(saved);
        Assert.Empty(saved.Tags);
        Assert.Equal(saved, Stored(shown));
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Oat milk", saved.Description);
    }

    [Fact]
    public void Choosing_a_tag_replaces_every_tag_of_a_task_saved_with_several()
    {
        var shown = _tasks.AddOlder("Buy milk", "", "home", "errand", "milk");

        var saved = _changes.Choose(shown, "urgent");

        Assert.NotNull(saved);
        Assert.Equal(["urgent"], saved.Tags);
        Assert.Equal(["urgent"], StoredTags(shown));
    }

    [Fact]
    public void Choosing_the_second_tag_of_a_task_saved_with_several_replaces_all_of_them_with_it()
    {
        var shown = _tasks.AddOlder("Buy milk", "", "home", "errand");

        _changes.Choose(shown, "errand");

        Assert.Equal(["errand"], StoredTags(shown));
    }

    [Fact]
    public void Clearing_removes_every_tag_of_a_task_saved_with_several()
    {
        var shown = _tasks.AddOlder("Buy milk", "", "home", "errand");

        var saved = _changes.Clear(shown);

        Assert.NotNull(saved);
        Assert.Empty(StoredTags(shown));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_title_changed_on_another_device_meanwhile_survives_the_tag_change(bool choose)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        RetitleElsewhere(shown, "Buy oat milk");

        if (choose)
            _changes.Choose(shown, "errand");
        else
            _changes.Clear(shown);

        var stored = Stored(shown);
        Assert.Equal("Buy oat milk", stored.Title);
        string[] expected = choose ? ["errand"] : [];
        Assert.Equal(expected, stored.Tags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_tag_chosen_on_another_device_meanwhile_is_replaced_by_the_change(bool choose)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        RetagElsewhere(shown, "urgent");

        if (choose)
            _changes.Choose(shown, "errand");
        else
            _changes.Clear(shown);

        string[] expected = choose ? ["errand"] : [];
        Assert.Equal(expected, StoredTags(shown));
    }

    // ---- The Undo bar ----

    [Fact]
    public void Choosing_a_tag_shows_the_Tag_changed_bar_for_the_same_time_as_a_delete()
    {
        var shown = _tasks.Add("Buy milk");

        _changes.Choose(shown, "home");

        Assert.True(_pending.IsPending);
        Assert.Equal(Changed, _pending.Label);
        _tasks.Time.Advance(BarEnds - TimeSpan.FromSeconds(1));
        Assert.True(_pending.IsPending);
        _tasks.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void Replacing_a_tag_also_says_Tag_changed()
    {
        var shown = _tasks.Add("Buy milk", "", "home");

        _changes.Choose(shown, "errand");

        Assert.Equal(Changed, _pending.Label);
    }

    [Fact]
    public void Clearing_the_tag_shows_the_Tag_cleared_bar()
    {
        var shown = _tasks.Add("Buy milk", "", "home");

        _changes.Clear(shown);

        Assert.True(_pending.IsPending);
        Assert.Equal(Cleared, _pending.Label);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_change_stays_when_the_bar_ends(bool choose)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var saved = choose ? _changes.Choose(shown, "errand") : _changes.Clear(shown);
        string[] expected = choose ? ["errand"] : [];

        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.NotNull(saved);
        Assert.Equal(expected, saved.Tags);
        Assert.Equal(expected, StoredTags(shown));
        Assert.Empty(_reports);
    }

    [Fact]
    public void Undo_of_a_chosen_tag_restores_the_tag_the_pill_showed_before()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _changes.Choose(shown, "errand");

        _pending.Undo();

        Assert.Equal(["home"], StoredTags(shown));
        Assert.False(_pending.IsPending);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Undo_of_a_tag_chosen_for_a_task_with_none_clears_it_again()
    {
        var shown = _tasks.Add("Buy milk");
        _changes.Choose(shown, "errand");

        _pending.Undo();

        Assert.Empty(StoredTags(shown));
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void Undo_of_a_cleared_tag_puts_it_back()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _changes.Clear(shown);

        _pending.Undo();

        Assert.Equal(["home"], StoredTags(shown));
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void Undo_of_a_choice_over_several_older_tags_brings_back_only_the_first()
    {
        var shown = _tasks.AddOlder("Buy milk", "", "home", "errand", "milk");
        _changes.Choose(shown, "urgent");

        _pending.Undo();

        Assert.Equal(["home"], StoredTags(shown));
    }

    [Fact]
    public void Undo_of_clearing_several_older_tags_brings_back_only_the_first()
    {
        var shown = _tasks.AddOlder("Buy milk", "", "home", "errand");
        _changes.Clear(shown);

        _pending.Undo();

        Assert.Equal(["home"], StoredTags(shown));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Undo_changes_only_the_tag_so_a_title_changed_elsewhere_meanwhile_stays(bool choose)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var saved = (choose ? _changes.Choose(shown, "errand") : _changes.Clear(shown))!;
        RetitleElsewhere(saved, "Buy oat milk");

        _pending.Undo();

        var stored = Stored(shown);
        Assert.Equal("Buy oat milk", stored.Title);
        Assert.Equal(["home"], stored.Tags);
    }

    [Fact]
    public void Undo_does_nothing_more_once_the_bar_is_gone_and_leaves_no_second_save()
    {
        var shown = _tasks.Add("Buy milk");
        _changes.Choose(shown, "home");
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
        _changes.Choose(first, "one");

        var saved = _changes.Choose(sameTask ? Stored(first) : other, "two");
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
        _changes.Choose(shown, "home");
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
        _changes.Choose(shown, "home");

        _cleanup.Leave(edited.Id, hasChanges: true, () => { });

        Assert.Equal("Edit discarded", _pending.Label);
        Assert.Equal(["home"], StoredTags(shown));
    }

    [Fact]
    public void Starting_an_edit_of_the_task_finishes_its_tag_bar_and_keeps_the_change()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _changes.Clear(shown);

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
        _changes.Choose(shown, "home");

        _cleanup.BeginFresh(other.Id);

        Assert.True(_pending.IsPending);
        Assert.Equal(Changed, _pending.Label);
        _pending.Undo();
        Assert.Empty(StoredTags(shown));
    }

    // ---- The images waiting for the task ----

    private IReadOnlyList<string> ImagesInClone() => _tasks.Files.AllFilesUnder(Path.Combine(_tasks.Clone, "images"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_tag_change_outside_an_edit_moves_the_images_still_waiting_for_the_task_into_the_clone_with_its_save(bool choose)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var images = _tasks.PasteImage(shown.Id);

        var saved = choose ? _changes.Choose(shown, "errand") : _changes.Clear(shown);

        Assert.NotNull(saved);
        Assert.DoesNotContain(images, _tasks.Files.FileExists);
        Assert.Equal(images.Count, ImagesInClone().Count);
        Assert.Empty(_reports);
    }

    [Fact]
    public void A_tag_change_never_tries_to_delete_the_waiting_images_so_it_saves_even_when_they_cannot_be_deleted()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _tasks.StopDeleting(_tasks.PasteImage(shown.Id));

        var saved = _changes.Choose(shown, "errand");

        Assert.NotNull(saved);
        Assert.Equal(["errand"], StoredTags(shown));
        Assert.Empty(_reports);
    }

    [Fact]
    public void A_tag_change_first_finishes_the_tasks_discarded_edit_bar_and_deletes_its_images_so_the_save_finds_none_to_move()
    {
        var shown = _tasks.Add("Buy milk");
        var images = _tasks.PasteImage(shown.Id);
        var restored = 0;
        _cleanup.Leave(shown.Id, hasChanges: true, () => restored++);

        var saved = _changes.Choose(shown, "home");

        Assert.NotNull(saved);
        Assert.Equal(Changed, _pending.Label);
        Assert.DoesNotContain(images, _tasks.Files.FileExists);
        Assert.Empty(ImagesInClone());
        Assert.Equal(0, restored);
        Assert.Empty(_reports);
    }

    // The earlier bar is finished before the save, so a save that fails leaves no bar behind.
    [Fact]
    public void A_tag_change_finishes_the_earlier_bar_before_it_saves_so_a_failed_save_leaves_the_earlier_change_kept_and_no_bar()
    {
        var first = _tasks.Add("First");
        var second = _tasks.Add("Second", "", "home");
        _changes.Choose(first, "one");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var saved = _changes.Choose(second, "two");

        Assert.Null(saved);
        Assert.False(_pending.IsPending);
        _tasks.Files.StopFailing();
        _pending.Undo();
        Assert.Equal(["one"], StoredTags(first));
        Assert.Equal(["home"], StoredTags(second));
    }


    // ---- Failures ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_failed_save_changes_nothing_shows_no_bar_and_says_why(bool choose)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var saved = choose ? _changes.Choose(shown, "errand") : _changes.Clear(shown);

        Assert.Null(saved);
        Assert.Equal(["home"], StoredTags(shown));
        Assert.False(_pending.IsPending);
        Assert.Contains("Writing is not allowed here", Assert.Single(_reports));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_task_deleted_meanwhile_says_it_no_longer_exists_and_shows_no_bar(bool choose)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _tasks.Tasks.Delete(shown.Id);

        var saved = choose ? _changes.Choose(shown, "errand") : _changes.Clear(shown);

        Assert.Null(saved);
        Assert.Equal("This task no longer exists.", Assert.Single(_reports));
        Assert.False(_pending.IsPending);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_failed_Undo_keeps_the_change_removes_the_bar_and_says_why(bool choose)
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var saved = (choose ? _changes.Choose(shown, "errand") : _changes.Clear(shown))!;
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
        _changes.Choose(shown, "home");
        _tasks.Tasks.Delete(shown.Id);

        _pending.Undo();

        Assert.Equal("This task no longer exists.", Assert.Single(_reports));
        Assert.False(_pending.IsPending);
    }
}
