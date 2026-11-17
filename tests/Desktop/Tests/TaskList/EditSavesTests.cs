using System.IO;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What saving a Title edit or a Description edit asks of Tasks: one save of the edited text and the pill's tag together, with
/// everything else as the task was. The tests give the save the tag the pill shows, as the window does, and read what Tasks
/// keeps and how many saves it saw.
/// </summary>
[Unit]
public class EditSavesTests
{
    private readonly ListTasks _tasks = new();
    private int _saves;

    private TaskItem Stored(TaskItem task) => _tasks.Tasks.All.Single(t => t.Id == task.Id);

    // Counts the saves Tasks makes from now on, one notice for each.
    private void CountSaves() => _tasks.Tasks.Changed += (_, _) => _saves++;

    private void SetTagElsewhere(TaskItem started, string tag) =>
        _tasks.Tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, started.Description, [tag]));

    // ---- One save of the text and the tag ----

    [Fact]
    public void Saving_a_title_with_the_pills_tag_saves_both_in_one_save_and_changes_nothing_else()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        EditSaves.Title(_tasks.Tasks, started, "errand")("Buy oat milk");

        var stored = Stored(started);
        Assert.Equal("Buy oat milk", stored.Title);
        Assert.Equal(["errand"], stored.Tags);
        Assert.Equal("Oat milk", stored.Description);
        Assert.Equal(started.Rank, stored.Rank);
        Assert.Equal(1, _saves);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Saving_a_description_with_the_pills_tag_saves_both_in_one_save_and_changes_nothing_else()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        EditSaves.Description(_tasks.Tasks, started, "errand")("Oat milk, two cartons");

        var stored = Stored(started);
        Assert.Equal("Oat milk, two cartons", stored.Description);
        Assert.Equal(["errand"], stored.Tags);
        Assert.Equal("Buy milk", stored.Title);
        Assert.Equal(started.Rank, stored.Rank);
        Assert.Equal(1, _saves);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Saving_a_title_with_a_pill_that_shows_no_tag_clears_the_tag_in_the_same_save()
    {
        var started = _tasks.Add("Buy milk", "", "home");
        CountSaves();

        EditSaves.Title(_tasks.Tasks, started, null)("Buy oat milk");

        Assert.Equal("Buy oat milk", Stored(started).Title);
        Assert.Empty(Stored(started).Tags);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void Saving_a_description_with_a_pill_that_shows_no_tag_clears_the_tag_in_the_same_save()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        EditSaves.Description(_tasks.Tasks, started, null)("Oat milk, two cartons");

        Assert.Equal("Oat milk, two cartons", Stored(started).Description);
        Assert.Empty(Stored(started).Tags);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void Saving_a_description_with_the_tag_the_task_had_changes_only_the_description()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");

        EditSaves.Description(_tasks.Tasks, started, "home")("Oat milk, two cartons");

        var stored = Stored(started);
        Assert.Equal("Oat milk, two cartons", stored.Description);
        Assert.Equal(["home"], stored.Tags);
        Assert.Equal("Buy milk", stored.Title);
    }

    [Fact]
    public void Saving_a_title_with_the_tag_the_task_had_changes_only_the_title()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");

        EditSaves.Title(_tasks.Tasks, started, "home")("Buy oat milk");

        var stored = Stored(started);
        Assert.Equal("Buy oat milk", stored.Title);
        Assert.Equal(["home"], stored.Tags);
        Assert.Equal("Oat milk", stored.Description);
    }

    [Fact]
    public void A_pill_that_shows_no_tag_for_a_task_with_none_changes_nothing_but_the_text()
    {
        var started = _tasks.Add("Buy milk", "Oat milk");

        EditSaves.Description(_tasks.Tasks, started, null)("Oat milk, two cartons");

        Assert.Empty(Stored(started).Tags);
    }

    // ---- A change of the tag alone ----

    [Fact]
    public void A_changed_tag_alone_is_saved_in_one_save_with_the_text_as_it_was_for_a_description_edit()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        EditSaves.Description(_tasks.Tasks, started, "errand")("Oat milk");

        var stored = Stored(started);
        Assert.Equal(["errand"], stored.Tags);
        Assert.Equal("Oat milk", stored.Description);
        Assert.Equal("Buy milk", stored.Title);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void A_changed_tag_alone_is_saved_in_one_save_with_the_text_as_it_was_for_a_title_edit()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        EditSaves.Title(_tasks.Tasks, started, "errand")("Buy milk");

        Assert.Equal(["errand"], Stored(started).Tags);
        Assert.Equal("Buy milk", Stored(started).Title);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void A_cleared_tag_alone_is_saved_with_the_text_as_it_was()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");

        EditSaves.Description(_tasks.Tasks, started, null)("Oat milk");

        Assert.Empty(Stored(started).Tags);
        Assert.Equal("Oat milk", Stored(started).Description);
    }

    [Fact]
    public void Nothing_is_saved_when_neither_the_text_nor_the_tag_changed_not_even_for_a_tag_that_differs_only_in_case()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "Home");
        CountSaves();

        EditSaves.Description(_tasks.Tasks, started, "HOME")("Oat milk");

        Assert.Equal(started, Stored(started));
        Assert.Equal(0, _saves);
    }

    // ---- The tag another device set meanwhile ----

    [Fact]
    public void A_tag_chosen_on_another_device_meanwhile_stays_when_the_pill_still_shows_the_started_tag_on_a_title_save()
    {
        var started = _tasks.Add("Buy milk", "", "home");
        SetTagElsewhere(started, "urgent");

        EditSaves.Title(_tasks.Tasks, started, "home")("Buy oat milk");

        Assert.Equal(["urgent"], Stored(started).Tags);
        Assert.Equal("Buy oat milk", Stored(started).Title);
    }

    [Fact]
    public void A_tag_chosen_on_another_device_meanwhile_stays_when_the_pill_still_shows_the_started_tag_on_a_description_save()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        SetTagElsewhere(started, "urgent");

        EditSaves.Description(_tasks.Tasks, started, "home")("Oat milk, two cartons");

        Assert.Equal(["urgent"], Stored(started).Tags);
        Assert.Equal("Oat milk, two cartons", Stored(started).Description);
    }

    [Fact]
    public void A_tag_chosen_on_another_device_meanwhile_stays_when_the_task_had_none_and_the_pill_still_shows_none()
    {
        var started = _tasks.Add("Buy milk", "Oat milk");
        SetTagElsewhere(started, "urgent");

        EditSaves.Description(_tasks.Tasks, started, null)("Oat milk, two cartons");

        Assert.Equal(["urgent"], Stored(started).Tags);
    }

    [Fact]
    public void A_tag_chosen_on_another_device_meanwhile_is_replaced_by_the_one_the_user_chose_in_the_edit()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        SetTagElsewhere(started, "urgent");

        EditSaves.Description(_tasks.Tasks, started, "errand")("Oat milk, two cartons");

        Assert.Equal(["errand"], Stored(started).Tags);
    }

    [Fact]
    public void A_title_changed_on_another_device_meanwhile_stays_when_a_description_and_a_tag_are_saved()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        _tasks.Tasks.SaveEdit(started, new TaskDraft(started.Id, "Buy oat milk", started.Description, [.. started.Tags]));

        EditSaves.Description(_tasks.Tasks, started, "errand")("Oat milk, two cartons");

        Assert.Equal("Buy oat milk", Stored(started).Title);
        Assert.Equal(["errand"], Stored(started).Tags);
        Assert.Equal("Oat milk, two cartons", Stored(started).Description);
    }

    // ---- A task saved with several tags ----

    [Fact]
    public void A_task_saved_with_several_tags_keeps_all_of_them_when_the_pill_still_shows_the_first_one_on_a_title_save()
    {
        var started = _tasks.AddOlder("Buy milk", "Oat milk", "home", "errand");

        EditSaves.Title(_tasks.Tasks, started, "home")("Buy oat milk");

        Assert.Equal("Buy oat milk", Stored(started).Title);
        Assert.Equal(["home", "errand"], Stored(started).Tags);
    }

    [Fact]
    public void A_task_saved_with_several_tags_keeps_all_of_them_when_the_pill_still_shows_the_first_one_on_a_description_save()
    {
        var started = _tasks.AddOlder("Buy milk", "Oat milk", "home", "errand");

        EditSaves.Description(_tasks.Tasks, started, "home")("Oat milk, two cartons");

        Assert.Equal("Oat milk, two cartons", Stored(started).Description);
        Assert.Equal(["home", "errand"], Stored(started).Tags);
    }

    [Fact]
    public void A_tag_chosen_in_the_edit_replaces_every_tag_of_a_task_saved_with_several()
    {
        var started = _tasks.AddOlder("Buy milk", "Oat milk", "home", "errand");

        EditSaves.Description(_tasks.Tasks, started, "urgent")("Oat milk, two cartons");

        Assert.Equal(["urgent"], Stored(started).Tags);
    }

    [Fact]
    public void A_tag_cleared_in_the_edit_clears_every_tag_of_a_task_saved_with_several()
    {
        var started = _tasks.AddOlder("Buy milk", "Oat milk", "home", "errand");

        EditSaves.Title(_tasks.Tasks, started, null)("Buy oat milk");

        Assert.Empty(Stored(started).Tags);
    }

    // ---- Failures ----

    [Fact]
    public void A_failed_save_propagates_for_the_editor_to_report_and_saves_neither_the_text_nor_the_tag()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var failure = Assert.Throws<IOException>(() => EditSaves.Title(_tasks.Tasks, started, "errand")("Buy oat milk"));

        Assert.StartsWith("Writing is not allowed here", failure.Message);
        Assert.Equal(started, Stored(started));
    }

    [Fact]
    public void An_empty_title_is_refused_and_the_tag_is_not_saved_either()
    {
        var started = _tasks.Add("Buy milk", "", "home");

        Assert.Throws<ArgumentException>(() => EditSaves.Title(_tasks.Tasks, started, "errand")(""));

        Assert.Equal(["home"], Stored(started).Tags);
        Assert.Equal("Buy milk", Stored(started).Title);
    }

    [Fact]
    public void A_task_deleted_meanwhile_is_refused_so_the_save_never_undoes_the_deletion()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        _tasks.Tasks.Delete(started.Id);

        Assert.Throws<TaskNotFoundException>(() => EditSaves.Description(_tasks.Tasks, started, "errand")("Oat milk, two cartons"));
        Assert.Empty(_tasks.Tasks.All);
    }

    // ---- The images waiting for the task ----

    [Fact]
    public void Saving_a_description_with_a_tag_moves_the_waiting_images_into_the_clone_in_that_one_save()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        var images = _tasks.PasteImage(started.Id);

        EditSaves.Description(_tasks.Tasks, started, "errand")("Oat milk, two cartons");

        Assert.DoesNotContain(images, _tasks.Files.FileExists);
        Assert.Equal(images.Count, _tasks.Files.AllFilesUnder(Path.Combine(_tasks.Clone, "images")).Count);
        Assert.Equal(["errand"], Stored(started).Tags);
    }

    [Fact]
    public void Saving_a_tag_alone_still_moves_the_waiting_images_into_the_clone()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        var images = _tasks.PasteImage(started.Id);

        EditSaves.Description(_tasks.Tasks, started, "errand")("Oat milk");

        Assert.DoesNotContain(images, _tasks.Files.FileExists);
        Assert.Equal(images.Count, _tasks.Files.AllFilesUnder(Path.Combine(_tasks.Clone, "images")).Count);
    }

    // ---- When the tag counts as changed ----

    [Theory]
    [InlineData("home", "errand", true)]
    [InlineData("home", "home", false)]
    [InlineData("Home", "HOME", false)]
    [InlineData("home", null, true)]
    [InlineData(null, "home", true)]
    [InlineData(null, null, false)]
    public void The_tag_counts_as_changed_when_it_differs_from_the_started_one_ignoring_case_where_no_tag_is_a_value(string? started, string? pill, bool changed)
    {
        var task = started is null ? _tasks.Add("Buy milk") : _tasks.Add("Buy milk", "", started);

        Assert.Equal(changed, EditSaves.TagChanged(task, pill));
    }

    [Fact]
    public void A_task_saved_with_several_tags_counts_as_unchanged_for_its_first_tag_and_changed_for_any_other()
    {
        var task = _tasks.AddOlder("Buy milk", "", "home", "errand");

        Assert.False(EditSaves.TagChanged(task, "home"));
        Assert.True(EditSaves.TagChanged(task, "errand"));
        Assert.True(EditSaves.TagChanged(task, null));
    }
}
