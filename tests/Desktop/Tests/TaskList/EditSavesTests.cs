using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What saving a Title edit or a Description edit asks of Tasks: one save of the edited text and the tag control's whole list
/// together, with everything else as the task was. The tests give the save the list the control shows, as the window does, and
/// read what Tasks keeps and how many saves it saw.
/// </summary>
[Unit]
public class EditSavesTests
{
    private readonly ListTasks _tasks = new();
    private readonly List<string> _reports = [];
    private readonly InMemoryLogSink _log = new();
    private int _saves;

    // The list's message line takes an Action<string>, the same report ListFailures.Run and ListActions take.
    private Action<string> Title(TaskItem started, IReadOnlyList<string> tags) =>
        EditSaves.Title(_tasks.Tasks, started, tags, _reports.Add, _log.CreateLogger());

    private Action<string> Description(TaskItem started, IReadOnlyList<string> tags) =>
        EditSaves.Description(_tasks.Tasks, started, tags, _reports.Add, _log.CreateLogger());

    private TaskItem Stored(TaskItem task) => _tasks.Tasks.All.Single(t => t.Id == task.Id);

    // Counts the saves Tasks makes from now on, one notice for each.
    private void CountSaves() => _tasks.Tasks.Changed += (_, _) => _saves++;

    private void SetTagsElsewhere(TaskItem started, params string[] tags) =>
        _tasks.Tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, started.Description, tags));

    // ---- One save of the text and the tags ----

    [Fact]
    public void Saving_a_title_with_the_controls_tags_saves_both_in_one_save_and_changes_nothing_else()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        Title(started, ["errand", "home"])("Buy oat milk");

        var stored = Stored(started);
        Assert.Equal("Buy oat milk", stored.Title);
        Assert.Equal(["errand", "home"], stored.Tags);
        Assert.Equal("Oat milk", stored.Description);
        Assert.Equal(started.Rank, stored.Rank);
        Assert.Equal(1, _saves);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Saving_a_description_with_the_controls_tags_saves_both_in_one_save_and_changes_nothing_else()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        Description(started, ["errand", "home"])("Oat milk, two cartons");

        var stored = Stored(started);
        Assert.Equal("Oat milk, two cartons", stored.Description);
        Assert.Equal(["errand", "home"], stored.Tags);
        Assert.Equal("Buy milk", stored.Title);
        Assert.Equal(started.Rank, stored.Rank);
        Assert.Equal(1, _saves);
        Assert.Single(_tasks.Tasks.All);
    }

    [Fact]
    public void Saving_with_every_tag_removed_clears_the_tags_in_the_same_save()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home", "errand");
        CountSaves();

        Description(started, [])("Oat milk, two cartons");

        Assert.Equal("Oat milk, two cartons", Stored(started).Description);
        Assert.Empty(Stored(started).Tags);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void Saving_with_the_tags_the_task_had_changes_only_the_text()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home", "errand");

        Title(started, ["home", "errand"])("Buy oat milk");
        Description(Stored(started), ["home", "errand"])("Oat milk, two cartons");

        var stored = Stored(started);
        Assert.Equal("Buy oat milk", stored.Title);
        Assert.Equal("Oat milk, two cartons", stored.Description);
        Assert.Equal(["home", "errand"], stored.Tags);
    }

    [Fact]
    public void Saving_a_task_with_no_tags_and_a_control_with_none_changes_nothing_but_the_text()
    {
        var started = _tasks.Add("Buy milk", "Oat milk");

        Description(started, [])("Oat milk, two cartons");

        Assert.Empty(Stored(started).Tags);
    }

    // ---- A change of the tags alone ----

    [Fact]
    public void A_changed_list_alone_is_saved_in_one_save_with_the_text_as_it_was_for_a_description_edit()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        Description(started, ["errand"])("Oat milk");

        var stored = Stored(started);
        Assert.Equal(["errand"], stored.Tags);
        Assert.Equal("Oat milk", stored.Description);
        Assert.Equal("Buy milk", stored.Title);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void A_changed_list_alone_is_saved_in_one_save_with_the_text_as_it_was_for_a_title_edit()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        CountSaves();

        Title(started, ["errand"])("Buy milk");

        Assert.Equal(["errand"], Stored(started).Tags);
        Assert.Equal("Buy milk", Stored(started).Title);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void A_reorder_alone_is_saved_with_the_text_as_it_was()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home", "errand", "milk");
        CountSaves();

        Description(started, ["milk", "home", "errand"])("Oat milk");

        Assert.Equal(["milk", "home", "errand"], Stored(started).Tags);
        Assert.Equal("Oat milk", Stored(started).Description);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void A_reorder_changes_the_group_tag_the_task_is_saved_with()
    {
        var started = _tasks.Add("Buy milk", "", "home", "errand");

        Title(started, ["errand", "home"])("Buy milk");

        Assert.Equal("errand", Stored(started).GroupTag);
    }

    [Fact]
    public void Nothing_is_saved_when_neither_the_text_nor_the_list_changed_not_even_for_tags_that_differ_only_in_case()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home", "errand");
        CountSaves();

        Description(started, ["HOME", "Errand"])("Oat milk");

        Assert.Equal(started, Stored(started));
        Assert.Equal(0, _saves);
    }

    // ---- The tags another device set meanwhile ----

    [Fact]
    public void Tags_set_on_another_device_meanwhile_stay_when_the_list_is_as_the_edit_started_on_a_title_save()
    {
        var started = _tasks.Add("Buy milk", "", "home");
        SetTagsElsewhere(started, "urgent", "home");

        Title(started, ["home"])("Buy oat milk");

        Assert.Equal(["urgent", "home"], Stored(started).Tags);
        Assert.Equal("Buy oat milk", Stored(started).Title);
    }

    [Fact]
    public void Tags_set_on_another_device_meanwhile_stay_when_the_list_is_as_the_edit_started_on_a_description_save()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        SetTagsElsewhere(started, "urgent");

        Description(started, ["home"])("Oat milk, two cartons");

        Assert.Equal(["urgent"], Stored(started).Tags);
        Assert.Equal("Oat milk, two cartons", Stored(started).Description);
    }

    [Fact]
    public void Tags_set_on_another_device_meanwhile_stay_when_the_task_had_none_and_the_control_still_shows_none()
    {
        var started = _tasks.Add("Buy milk", "Oat milk");
        SetTagsElsewhere(started, "urgent");

        Description(started, [])("Oat milk, two cartons");

        Assert.Equal(["urgent"], Stored(started).Tags);
    }

    [Fact]
    public void Tags_set_on_another_device_meanwhile_are_replaced_by_the_list_the_user_made_in_the_edit()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        SetTagsElsewhere(started, "urgent");

        Description(started, ["errand"])("Oat milk, two cartons");

        Assert.Equal(["errand"], Stored(started).Tags);
    }

    [Fact]
    public void A_title_changed_on_another_device_meanwhile_stays_when_a_description_and_tags_are_saved()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        _tasks.Tasks.SaveEdit(started, new TaskDraft(started.Id, "Buy oat milk", started.Description, [.. started.Tags]));

        Description(started, ["errand", "home"])("Oat milk, two cartons");

        Assert.Equal("Buy oat milk", Stored(started).Title);
        Assert.Equal(["errand", "home"], Stored(started).Tags);
        Assert.Equal("Oat milk, two cartons", Stored(started).Description);
    }

    // ---- Failures ----

    [Fact]
    public void A_failed_save_propagates_for_the_editor_to_report_and_saves_neither_the_text_nor_the_tags()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        _tasks.Files.FailWritesUnder(_tasks.TaskFolder);

        var failure = Assert.Throws<IOException>(() => Title(started, ["errand", "home"])("Buy oat milk"));

        Assert.StartsWith("Writing is not allowed here", failure.Message);
        Assert.Equal(started, Stored(started));
    }

    [Fact]
    public void A_save_Tasks_kept_before_a_notice_failed_does_not_throw_reports_the_shared_message_and_logs_the_failure_once()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        _tasks.NoticesFail = true;

        Title(started, ["errand", "home"])("Buy oat milk");

        Assert.Equal("Buy oat milk", Stored(started).Title);
        Assert.Equal(["Saved, but the app couldn't finish updating: Changed failed."], _reports);
        Assert.DoesNotContain("wasn't saved", _reports[0]);
        var entry = Assert.Single(_log.Events, e => e.Level == Serilog.Events.LogEventLevel.Error);
        Assert.IsAssignableFrom<NoticeFailedException>(entry.Exception);
    }

    [Fact]
    public void A_description_save_Tasks_kept_before_a_notice_failed_does_not_throw_and_reports_the_shared_message_once()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        _tasks.NoticesFail = true;

        Description(started, ["errand", "home"])("Oat milk, two cartons");

        var stored = Stored(started);
        Assert.Equal("Oat milk, two cartons", stored.Description);
        Assert.Equal(["errand", "home"], stored.Tags);
        Assert.Equal(["Saved, but the app couldn't finish updating: Changed failed."], _reports);
        Assert.Single(_log.Events, e => e.Level == Serilog.Events.LogEventLevel.Error);
    }

    [Fact]
    public void An_empty_title_is_refused_and_the_tags_are_not_saved_either()
    {
        var started = _tasks.Add("Buy milk", "", "home");

        Assert.Throws<ArgumentException>(() => Title(started, ["errand"])(""));

        Assert.Equal(["home"], Stored(started).Tags);
        Assert.Equal("Buy milk", Stored(started).Title);
    }

    [Fact]
    public void A_task_deleted_meanwhile_is_refused_so_the_save_never_undoes_the_deletion()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        _tasks.Tasks.Delete(started.Id);

        Assert.Throws<TaskNotFoundException>(() => Description(started, ["errand"])("Oat milk, two cartons"));
        Assert.Empty(_tasks.Tasks.All);
    }

    // ---- The images waiting for the task ----

    [Fact]
    public void Saving_a_description_with_tags_moves_the_waiting_images_into_the_clone_in_that_one_save()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        var images = _tasks.PasteImage(started.Id);

        Description(started, ["errand", "home"])("Oat milk, two cartons");

        Assert.DoesNotContain(images, _tasks.Files.FileExists);
        Assert.Equal(images.Count, _tasks.Files.AllFilesUnder(Path.Combine(_tasks.Clone, "images")).Count);
        Assert.Equal(["errand", "home"], Stored(started).Tags);
    }

    [Fact]
    public void Saving_a_list_alone_still_moves_the_waiting_images_into_the_clone()
    {
        var started = _tasks.Add("Buy milk", "Oat milk", "home");
        var images = _tasks.PasteImage(started.Id);

        Description(started, ["errand"])("Oat milk");

        Assert.DoesNotContain(images, _tasks.Files.FileExists);
        Assert.Equal(images.Count, _tasks.Files.AllFilesUnder(Path.Combine(_tasks.Clone, "images")).Count);
    }

    // ---- When the list counts as changed ----

    [Theory]
    [InlineData("home,errand", "home,errand", false)]
    [InlineData("home,errand", "errand,home", true)]
    [InlineData("home,errand", "home", true)]
    [InlineData("home", "home,errand", true)]
    [InlineData("home", "errand", true)]
    [InlineData("home", "", true)]
    [InlineData("", "home", true)]
    [InlineData("", "", false)]
    [InlineData("Home,Errand", "HOME,errand", false)]
    public void The_list_counts_as_changed_when_it_differs_from_the_started_one_in_tags_or_order_ignoring_case(string started, string shown, bool changed)
    {
        var task = _tasks.Add("Buy milk", "", Split(started));

        Assert.Equal(changed, EditSaves.TagsChanged(task, Split(shown)));
    }

    private static string[] Split(string tags) => tags.Length == 0 ? [] : tags.Split(',');
}
