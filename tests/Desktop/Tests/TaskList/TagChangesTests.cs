using System.IO;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Changing the tags of the selected task from the list, with no Edit: each change saves at once through Tasks' save of an
/// edit, with the task as the list shows it and the control's whole new list, then waits on the Undo bar, which says what kind
/// of change it was and saves the whole previous list, in its order. The tests ask for a change the way the window does, with
/// the task as shown, and see what Tasks keeps, what the Undo bar says, what is reported, and what happens to the images still
/// waiting for the task.
/// </summary>
[Unit]
public class TagChangesTests
{
    private static readonly TimeSpan BarEnds = TimeSpan.FromSeconds(10);

    private readonly ListTasks _tasks = new();
    private readonly PendingUndo _pending;
    private readonly EditCleanup _cleanup;
    private readonly List<string> _reports = [];
    private readonly TagChanges _changes;

    public TagChangesTests()
    {
        _pending = new PendingUndo(_tasks.Time, post => post(), () => { });
        _cleanup = new EditCleanup(_tasks.Tasks, _pending, _reports.Add, Serilog.Core.Logger.None);
        _changes = new TagChanges(_tasks.Tasks, _pending, _cleanup, _reports.Add, Serilog.Core.Logger.None);
    }

    private TaskItem Stored(TaskItem task) => _tasks.Tasks.All.Single(t => t.Id == task.Id);

    private IReadOnlyList<string> StoredTags(TaskItem task) => Stored(task).Tags;

    private TaskItem? Change(TaskItem shown, params string[] tags) => _changes.Change(shown, tags);

    private static string[] Split(string tags) => tags.Length == 0 ? [] : tags.Split(',');

    // Another device saves a new title for the task, which reaches this one while the list still shows the old task.
    private void RetitleElsewhere(TaskItem shown, string title) =>
        _tasks.Tasks.SaveEdit(shown, new TaskDraft(shown.Id, title, shown.Description, [.. shown.Tags]));

    // Another device saves other tags, which reach this one while the list still shows the old task.
    private void RetagElsewhere(TaskItem shown, params string[] tags) =>
        _tasks.Tasks.SaveEdit(shown, new TaskDraft(shown.Id, shown.Title, shown.Description, tags));

    // ---- The save ----

    [Fact]
    public void Adding_a_tag_saves_the_whole_list_with_it_last_and_nothing_else_changed()
    {
        var shown = _tasks.Add("Buy milk", "Oat milk", "home");

        var saved = Change(shown, "home", "errand");

        Assert.NotNull(saved);
        Assert.Equal(["home", "errand"], saved.Tags);
        Assert.Equal(saved, Stored(shown));
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal("Oat milk", saved.Description);
        Assert.Equal(shown.Rank, saved.Rank);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Adding_a_tag_to_a_task_with_none_makes_it_the_first_tag()
    {
        var shown = _tasks.Add("Buy milk");

        var saved = Change(shown, "errand");

        Assert.NotNull(saved);
        Assert.Equal(["errand"], saved.Tags);
        Assert.Equal("errand", saved.GroupTag);
    }

    [Fact]
    public void Removing_a_tag_saves_the_list_without_it_and_keeps_the_others_in_order()
    {
        var shown = _tasks.Add("Buy milk", "", "home", "errand", "milk");

        var saved = Change(shown, "home", "milk");

        Assert.NotNull(saved);
        Assert.Equal(["home", "milk"], StoredTags(shown));
    }

    [Fact]
    public void Removing_the_last_tag_saves_the_task_with_no_tag()
    {
        var shown = _tasks.Add("Buy milk", "Oat milk", "home");

        var saved = Change(shown);

        Assert.NotNull(saved);
        Assert.Empty(saved.Tags);
        Assert.Null(saved.GroupTag);
        Assert.Equal(saved, Stored(shown));
        Assert.Equal("Oat milk", saved.Description);
    }

    [Fact]
    public void Replacing_a_tag_saves_the_list_with_the_new_tag_in_its_place()
    {
        var shown = _tasks.Add("Buy milk", "", "home", "errand");

        var saved = Change(shown, "urgent", "errand");

        Assert.NotNull(saved);
        Assert.Equal(["urgent", "errand"], StoredTags(shown));
        Assert.Equal("urgent", Stored(shown).GroupTag);
    }

    [Fact]
    public void Reordering_the_tags_saves_the_new_order_and_the_first_tag_groups_the_task()
    {
        var shown = _tasks.Add("Buy milk", "", "home", "errand", "milk");

        var saved = Change(shown, "milk", "home", "errand");

        Assert.NotNull(saved);
        Assert.Equal(["milk", "home", "errand"], StoredTags(shown));
        Assert.Equal("milk", saved.GroupTag);
        Assert.Equal(shown.Rank, saved.Rank);
    }

    [Fact]
    public void A_tag_is_saved_normalized_in_lowercase_with_hyphens()
    {
        var shown = _tasks.Add("Buy milk");

        var saved = Change(shown, "Home Office");

        Assert.NotNull(saved);
        Assert.Equal(["home-office"], saved.Tags);
    }

    [Fact]
    public void A_title_changed_on_another_device_meanwhile_survives_the_tag_change()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        RetitleElsewhere(shown, "Buy oat milk");

        Change(shown, "home", "errand");

        var stored = Stored(shown);
        Assert.Equal("Buy oat milk", stored.Title);
        Assert.Equal(["home", "errand"], stored.Tags);
    }

    [Fact]
    public void Tags_set_on_another_device_meanwhile_are_replaced_by_the_change()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        RetagElsewhere(shown, "urgent");

        Change(shown, "home", "errand");

        Assert.Equal(["home", "errand"], StoredTags(shown));
    }

    // ---- The Undo bar says what kind of change it was ----

    [Theory]
    [InlineData("", "home", "Tag added")]
    [InlineData("home", "home,errand", "Tag added")]
    [InlineData("home,errand", "home", "Tag removed")]
    [InlineData("home,errand", "errand", "Tag removed")]
    [InlineData("home", "", "Tag removed")]
    [InlineData("home", "errand", "Tag changed")]
    [InlineData("home,errand", "urgent,errand", "Tag changed")]
    [InlineData("home,errand", "home,urgent", "Tag changed")]
    [InlineData("home,errand", "errand,home", "Tags reordered")]
    [InlineData("home,errand,milk", "milk,home,errand", "Tags reordered")]
    public void The_bar_says_which_kind_of_change_it_was(string before, string after, string label)
    {
        var shown = _tasks.Add("Buy milk", "", Split(before));

        Change(shown, Split(after));

        Assert.True(_pending.IsPending);
        Assert.Equal(label, _pending.Label);
    }

    [Fact]
    public void A_tag_change_shows_its_bar_for_the_same_time_as_a_delete()
    {
        var shown = _tasks.Add("Buy milk");

        Change(shown, "home");

        Assert.True(_pending.IsPending);
        _tasks.Time.Advance(BarEnds - TimeSpan.FromSeconds(1));
        Assert.True(_pending.IsPending);
        _tasks.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_pending.IsPending);
    }

    [Theory]
    [InlineData("", "home")]
    [InlineData("home,errand", "home")]
    [InlineData("home", "errand")]
    [InlineData("home,errand", "errand,home")]
    public void The_change_stays_when_the_bar_ends(string before, string after)
    {
        var shown = _tasks.Add("Buy milk", "", Split(before));
        var saved = Change(shown, Split(after));

        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.NotNull(saved);
        Assert.Equal(Split(after), StoredTags(shown));
        Assert.Empty(_reports);
    }

    // ---- Undo restores the whole previous list in order ----

    [Theory]
    [InlineData("", "home")]
    [InlineData("home", "home,errand")]
    [InlineData("home,errand,milk", "home,milk")]
    [InlineData("home,errand,milk", "errand,milk")]
    [InlineData("home", "")]
    [InlineData("home,errand,milk", "")]
    [InlineData("home,errand", "urgent,errand")]
    [InlineData("home,errand,milk", "milk,home,errand")]
    [InlineData("home,errand", "errand,home")]
    public void Undo_restores_the_whole_previous_list_in_its_order(string before, string after)
    {
        var shown = _tasks.Add("Buy milk", "", Split(before));
        Change(shown, Split(after));

        _pending.Undo();

        Assert.Equal(Split(before), StoredTags(shown));
        Assert.False(_pending.IsPending);
        Assert.Empty(_reports);
    }

    [Fact]
    public void Undo_of_a_change_of_the_first_tag_puts_the_task_back_under_its_old_group_tag()
    {
        var shown = _tasks.Add("Buy milk", "", "home", "errand");
        Change(shown, "errand", "home");

        _pending.Undo();

        Assert.Equal("home", Stored(shown).GroupTag);
    }

    [Fact]
    public void Undo_changes_only_the_tags_so_a_title_changed_elsewhere_meanwhile_stays()
    {
        var shown = _tasks.Add("Buy milk", "", "home", "errand");
        var saved = Change(shown, "errand")!;
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
        Change(shown, "home");
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
        Change(first, "one");

        var saved = sameTask ? Change(Stored(first), "one", "two") : Change(other, "two");
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
        Change(shown, "home");
        var deletion = new TaskDeletion(_tasks.Tasks, new ListContent(_tasks.Tasks, new ListFilter(_tasks.Contacts)), _pending, _reports.Add, () => { }, Serilog.Core.Logger.None);

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
        Change(shown, "home");

        _cleanup.Leave(edited.Id, hasChanges: true, () => { });

        Assert.Equal("Edit discarded", _pending.Label);
        Assert.Equal(["home"], StoredTags(shown));
    }

    [Theory]
    [InlineData("", "home", "Tag added")]
    [InlineData("home,errand", "home", "Tag removed")]
    [InlineData("home", "errand", "Tag changed")]
    [InlineData("home,errand", "errand,home", "Tags reordered")]
    public void Starting_an_edit_of_the_task_finishes_its_tag_bar_of_any_kind_and_keeps_the_change(string before, string after, string label)
    {
        var shown = _tasks.Add("Buy milk", "", Split(before));
        Change(shown, Split(after));
        Assert.Equal(label, _pending.Label);

        var started = _cleanup.BeginFresh(shown.Id);
        _pending.Undo();

        Assert.True(started);
        Assert.False(_pending.IsPending);
        Assert.Equal(Split(after), StoredTags(shown));
    }

    [Fact]
    public void Starting_an_edit_of_another_task_leaves_the_tag_bar()
    {
        var shown = _tasks.Add("Buy milk");
        var other = _tasks.Add("Call Ana");
        Change(shown, "home");

        _cleanup.BeginFresh(other.Id);

        Assert.True(_pending.IsPending);
        Assert.Equal("Tag added", _pending.Label);
        _pending.Undo();
        Assert.Empty(StoredTags(shown));
    }

    // ---- The images waiting for the task ----

    private IReadOnlyList<string> ImagesInClone() => _tasks.Files.AllFilesUnder(Path.Combine(_tasks.Clone, "images"));

    [Fact]
    public void A_tag_change_outside_an_edit_moves_the_images_still_waiting_for_the_task_into_the_clone_with_its_save()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var images = _tasks.PasteImage(shown.Id);

        var saved = Change(shown, "home", "errand");

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

        var saved = Change(shown, "errand", "home");

        Assert.NotNull(saved);
        Assert.Equal(["errand", "home"], StoredTags(shown));
        Assert.Empty(_reports);
    }

    [Fact]
    public void A_tag_change_first_finishes_the_tasks_discarded_edit_bar_and_deletes_its_images_so_the_save_finds_none_to_move()
    {
        var shown = _tasks.Add("Buy milk");
        var images = _tasks.PasteImage(shown.Id);
        var restored = 0;
        _cleanup.Leave(shown.Id, hasChanges: true, () => restored++);

        var saved = Change(shown, "home");

        Assert.NotNull(saved);
        Assert.Equal("Tag added", _pending.Label);
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
        Change(first, "one");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var saved = Change(second, "two");

        Assert.Null(saved);
        Assert.False(_pending.IsPending);
        _tasks.Files.StopFailing();
        _pending.Undo();
        Assert.Equal(["one"], StoredTags(first));
        Assert.Equal(["home"], StoredTags(second));
    }

    // ---- Failures ----

    [Theory]
    [InlineData("", "home")]
    [InlineData("home,errand", "home")]
    [InlineData("home", "errand")]
    [InlineData("home,errand", "errand,home")]
    public void A_failed_save_changes_nothing_shows_no_bar_and_says_why(string before, string after)
    {
        var shown = _tasks.Add("Buy milk", "", Split(before));
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var saved = Change(shown, Split(after));

        Assert.Null(saved);
        Assert.Equal(Split(before), StoredTags(shown));
        Assert.False(_pending.IsPending);
        Assert.Contains("Writing is not allowed here", Assert.Single(_reports));
    }

    [Fact]
    public void A_task_deleted_meanwhile_says_it_no_longer_exists_and_shows_no_bar()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _tasks.Tasks.Delete(shown.Id);

        var saved = Change(shown, "home", "errand");

        Assert.Null(saved);
        Assert.Equal("This task no longer exists.", Assert.Single(_reports));
        Assert.False(_pending.IsPending);
    }

    [Fact]
    public void A_failed_Undo_keeps_the_change_removes_the_bar_and_says_why()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var saved = Change(shown, "home", "errand")!;
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        _pending.Undo();
        _tasks.Files.StopFailing();
        _tasks.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(saved.Tags, StoredTags(shown));
        Assert.False(_pending.IsPending);
        Assert.Contains("Writing is not allowed here", Assert.Single(_reports));
    }

    [Fact]
    public void A_tag_change_Tasks_saved_before_a_notice_failed_reports_that_and_still_shows_its_bar()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        _tasks.NoticesFail = true;

        var result = Change(shown, "home", "errand");

        Assert.Equal(["home", "errand"], StoredTags(shown));
        Assert.Equal(Stored(shown), result);
        Assert.True(_pending.IsPending);
        Assert.Equal("Tag added", _pending.Label);
        Assert.Equal(["Saved, but the app couldn't finish updating: Changed failed."], _reports);
    }

    [Fact]
    public void A_tag_change_Tasks_did_not_save_and_may_be_only_partly_undone_reports_that_and_shows_no_bar()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        var images = _tasks.PasteImage(shown.Id);
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);
        _tasks.Files.FailWritesUnder(Path.GetDirectoryName(images[0])!);

        var result = Change(shown, "home", "errand");

        Assert.Equal(["home"], StoredTags(shown));
        Assert.True(result is null || result == Stored(shown));
        Assert.False(_pending.IsPending);
        var report = Assert.Single(_reports);
        Assert.StartsWith("The change may be only partly saved: Writing is not allowed here", report);
    }

    [Fact]
    public void An_Undo_Tasks_saved_before_a_notice_failed_removes_the_bar_restores_the_old_tags_and_reports_that()
    {
        var shown = _tasks.Add("Buy milk", "", "home");
        Change(shown, "home", "errand");
        _tasks.NoticesFail = true;

        _pending.Undo();

        Assert.False(_pending.IsPending);
        Assert.Equal(["home"], StoredTags(shown));
        Assert.Equal(["Saved, but the app couldn't finish updating: Changed failed."], _reports);
    }

    [Fact]
    public void Undo_of_a_change_on_a_task_deleted_meanwhile_says_it_no_longer_exists()
    {
        var shown = _tasks.Add("Buy milk");
        Change(shown, "home");
        _tasks.Tasks.Delete(shown.Id);

        _pending.Undo();

        Assert.Equal("This task no longer exists.", Assert.Single(_reports));
        Assert.False(_pending.IsPending);
    }
}
