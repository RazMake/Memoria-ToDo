using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// After a change is saved, Tasks raises the notices the change calls for (Changed, then the settings', tag colors', and tag order's)
/// and reports the local save, each even when an earlier one failed, then throws by the platform's rule for steps that must all run.
/// A reload raises all four notices, reports no save, and throws by the same rule.
/// </summary>
[Unit]
public sealed class TaskNoticeFailureTests
{
    private const string Open = "01900000-0000-7000-8000-000000000001";
    private const string Done = "01900000-0000-7000-8000-000000000002";
    private const string Expired = "01900000-0000-7000-8000-000000000003";

    private readonly TasksFixture f = new();
    private readonly List<string> log = [];

    public TaskNoticeFailureTests()
    {
        f.Seed(Open, "Open", rank: "a0", tags: ["work"]);
        f.Seed(Done, "Done", rank: "a1", tags: ["home"], resolved: "2026-11-11T08:30:15.123Z");
        f.Seed(Expired, "Expired", rank: "a2", resolved: "2020-01-01T00:00:00.000Z");
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Tasks.Changed += (_, _) => log.Add("Changed");
        f.Store.Settings.Changed += (_, _) => log.Add("Settings");
        f.Store.TagColors.Changed += (_, _) => log.Add("TagColors");
        f.Store.TagOrder.Changed += (_, _) => log.Add("TagOrder");
        f.Repository.LocalSaved += (_, _) => log.Add("LocalSaved");
    }

    public static IEnumerable<TheoryDataRow<Action<ITaskStore>>> Changes() =>
    [
        Change("SaveEdit", s => s.SaveEdit(s.All.Single(t => t.Id == Open), new TaskDraft(Open, "Edited", "", ["work"]))),
        Change("Move", s => s.Move(Expired, null, Open)),
        Change("Resolve", s => s.Resolve(Open)),
        Change("Reopen", s => s.Reopen(Done)),
        Change("Delete", s => s.Delete(Open)),
        Change("DeleteExpiredTasks", s => s.Synchronization.DeleteExpiredTasks()),
    ];

    private static TheoryDataRow<Action<ITaskStore>> Change(string name, Action<ITaskStore> change) =>
        new(change) { TestDisplayName = name };

    private static InvalidOperationException Failure(string name) => new(name);

    private InvalidOperationException FailChanged()
    {
        var failure = Failure("Changed");
        f.Tasks.Changed += (_, _) => throw failure;
        return failure;
    }

    private InvalidOperationException FailSettings()
    {
        var failure = Failure("Settings");
        f.Store.Settings.Changed += (_, _) => throw failure;
        return failure;
    }

    private InvalidOperationException FailColors()
    {
        var failure = Failure("TagColors");
        f.Store.TagColors.Changed += (_, _) => throw failure;
        return failure;
    }

    private InvalidOperationException FailOrder()
    {
        var failure = Failure("TagOrder");
        f.Store.TagOrder.Changed += (_, _) => throw failure;
        return failure;
    }

    private InvalidOperationException FailReport()
    {
        var failure = Failure("report");
        f.Repository.LocalSaved += (_, _) => throw failure;
        return failure;
    }

    private void PutCloneImage(string taskId) =>
        f.Files.WriteFile(Path.Combine(f.CloneImages(taskId), "x.png"), [1, 2, 3]);

    // After a change

    [Fact]
    public void Raises_Changed_then_reports_the_local_save_after_a_task_change()
    {
        f.Tasks.Add(f.Draft());

        Assert.Equal(["Changed", "LocalSaved"], log);
    }

    [Fact]
    public void Still_reports_the_local_save_and_throws_the_handler_s_exception_as_it_is_when_a_Changed_handler_fails_after_Add()
    {
        var failure = FailChanged();
        var draft = f.Draft("Added");

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Tasks.Add(draft));

        Assert.Same(failure, thrown);
        Assert.Equal(["Changed", "LocalSaved"], log);
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void Still_reports_the_local_save_and_throws_the_handler_s_exception_as_it_is_when_a_Changed_handler_fails(Action<ITaskStore> change)
    {
        var failure = FailChanged();

        var thrown = Assert.Throws<InvalidOperationException>(() => change(f.Store));

        Assert.Same(failure, thrown);
        Assert.Equal(["Changed", "LocalSaved"], log);
    }

    [Fact]
    public void Still_raises_Changed_and_throws_the_report_s_exception_as_it_is_when_the_local_save_report_fails()
    {
        var failure = FailReport();

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Tasks.Resolve(Open));

        Assert.Same(failure, thrown);
        Assert.Equal(["Changed", "LocalSaved"], log);
    }

    [Fact]
    public void Throws_an_AggregateException_with_Changed_s_failure_then_the_report_s_when_both_fail()
    {
        var changed = FailChanged();
        var report = FailReport();

        var thrown = Assert.Throws<AggregateException>(() => f.Tasks.Add(f.Draft()));

        Assert.Equal(new Exception[] { changed, report }, thrown.InnerExceptions);
    }

    [Fact]
    public void Keeps_a_change_saved_and_visible_when_a_notice_fails()
    {
        var draft = f.Draft("Added");
        FailChanged();

        Assert.Throws<InvalidOperationException>(() => f.Tasks.Add(draft));

        Assert.Contains("Added", f.Titles);
        Assert.True(f.Files.FileExists(f.TaskFile(draft.TaskId)));
    }

    [Fact]
    public void Skips_the_later_handlers_of_the_failing_event_but_still_reports_the_local_save()
    {
        FailChanged();
        f.Tasks.Changed += (_, _) => log.Add("Changed again");

        Assert.Throws<InvalidOperationException>(() => f.Tasks.Resolve(Open));

        Assert.Equal(["Changed", "LocalSaved"], log);
    }

    [Fact]
    public void Throws_only_the_first_failure_of_an_event_s_handlers()
    {
        var first = FailChanged();
        f.Tasks.Changed += (_, _) => throw Failure("second handler");

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Tasks.Resolve(Open));

        Assert.Same(first, thrown);
    }

    [Fact]
    public void Still_reports_the_local_save_and_keeps_the_setting_when_a_settings_handler_fails()
    {
        var failure = FailSettings();

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Store.Settings.SetDeletionDelayDays(3));

        Assert.Same(failure, thrown);
        Assert.Equal(["Settings", "LocalSaved"], log);
        Assert.Equal(3, f.Store.Settings.DeletionDelayDays);
    }

    [Fact]
    public void Throws_an_AggregateException_with_the_settings_failure_then_the_report_s_when_both_fail()
    {
        var settings = FailSettings();
        var report = FailReport();

        var thrown = Assert.Throws<AggregateException>(() => f.Store.Settings.SetSaveShortcut("Ctrl+K"));

        Assert.Equal(new Exception[] { settings, report }, thrown.InnerExceptions);
        Assert.Equal("Ctrl+K", f.Store.Settings.SaveShortcut);
    }

    [Fact]
    public void Still_reports_the_local_save_and_keeps_the_color_when_a_tag_colors_handler_fails()
    {
        var failure = FailColors();

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Store.TagColors.SetColor("work", "red"));

        Assert.Same(failure, thrown);
        Assert.Equal(["TagColors", "LocalSaved"], log);
        Assert.Equal("red", f.Store.TagColors.ColorOf("work"));
    }

    [Fact]
    public void Still_reports_the_local_save_and_keeps_the_new_order_when_a_tag_order_handler_fails()
    {
        var failure = FailOrder();

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Store.TagOrder.Move("work", null, "home"));

        Assert.Same(failure, thrown);
        Assert.Equal(["TagOrder", "LocalSaved"], log);
        Assert.Equal(["work", "home"], f.Store.TagOrder.Sort(["home", "work"]));
    }

    [Fact]
    public void Raises_nothing_and_reports_nothing_when_the_change_fails_before_saving_because_the_input_is_invalid()
    {
        var invalid = new TaskDraft("not an id", "Title", "", []);

        Assert.Throws<ArgumentException>(() => f.Tasks.Add(invalid));

        Assert.Empty(log);
    }

    [Fact]
    public void Raises_nothing_and_reports_nothing_when_the_change_fails_because_the_file_cannot_be_written()
    {
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.ThrowsAny<Exception>(() => f.Tasks.Resolve(Open));

        Assert.Empty(log);
    }

    // After a deletion whose images can't be deleted

    public static IEnumerable<TheoryDataRow<Action<ITaskStore>, string>> Deletions() =>
    [
        new((Action<ITaskStore>)(s => s.Delete(Open)), Open) { TestDisplayName = "Delete" },
        new((Action<ITaskStore>)(s => s.Synchronization.DeleteExpiredTasks()), Expired) { TestDisplayName = "DeleteExpiredTasks" },
    ];

    [Theory]
    [MemberData(nameof(Deletions))]
    public void Still_raises_Changed_and_reports_the_local_save_and_throws_the_deletion_s_failure_when_its_images_cannot_be_deleted(Action<ITaskStore> delete, string id)
    {
        PutCloneImage(id);
        f.Files.FailDeletesUnder(f.CloneImages(id));

        Assert.IsType<IOException>(Record.Exception(() => delete(f.Store)));

        Assert.Equal(["Changed", "LocalSaved"], log);
        Assert.DoesNotContain(id, f.Ids);
        Assert.False(f.Files.FileExists(f.TaskFile(id)));
    }

    [Theory]
    [MemberData(nameof(Deletions))]
    public void Throws_an_AggregateException_with_the_deletion_s_failure_then_Changed_s_then_the_report_s_when_all_fail(Action<ITaskStore> delete, string id)
    {
        PutCloneImage(id);
        f.Files.FailDeletesUnder(f.CloneImages(id));
        var changed = FailChanged();
        var report = FailReport();

        var thrown = Assert.Throws<AggregateException>(() => delete(f.Store));

        Assert.Equal(3, thrown.InnerExceptions.Count);
        Assert.IsType<IOException>(thrown.InnerExceptions[0]);
        Assert.Same(changed, thrown.InnerExceptions[1]);
        Assert.Same(report, thrown.InnerExceptions[2]);
        Assert.Equal(["Changed", "LocalSaved"], log);
    }

    [Theory]
    [MemberData(nameof(Deletions))]
    public void Throws_an_AggregateException_with_the_deletion_s_failure_then_Changed_s_when_the_report_succeeds(Action<ITaskStore> delete, string id)
    {
        PutCloneImage(id);
        f.Files.FailDeletesUnder(f.CloneImages(id));
        var changed = FailChanged();

        var thrown = Assert.Throws<AggregateException>(() => delete(f.Store));

        Assert.IsType<IOException>(thrown.InnerExceptions[0]);
        Assert.Same(changed, thrown.InnerExceptions[1]);
        Assert.Equal(2, thrown.InnerExceptions.Count);
        Assert.Equal(["Changed", "LocalSaved"], log);
    }

    // After a reload

    [Fact]
    public void Raises_all_four_notices_in_order_and_reports_no_local_save_after_a_reload()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Changed", "Settings", "TagColors", "TagOrder"], log);
    }

    [Fact]
    public void Still_raises_the_later_notices_reports_no_local_save_and_throws_the_failure_as_it_is_when_one_notice_fails_after_a_reload()
    {
        var failure = FailChanged();

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Tasks.UseRepositoryFolder(f.Clone));

        Assert.Same(failure, thrown);
        Assert.Equal(["Changed", "Settings", "TagColors", "TagOrder"], log);
    }

    [Fact]
    public void Throws_an_AggregateException_with_the_failures_in_the_order_they_happened_when_several_notices_fail_after_a_reload()
    {
        var order = FailOrder();
        var settings = FailSettings();

        var thrown = Assert.Throws<AggregateException>(() => f.Tasks.UseRepositoryFolder(f.Clone));

        Assert.Equal(new Exception[] { settings, order }, thrown.InnerExceptions);
        Assert.Equal(["Changed", "Settings", "TagColors", "TagOrder"], log);
    }

    [Fact]
    public void Keeps_the_reloaded_state_when_a_notice_fails_after_a_reload()
    {
        f.Seed(TasksFixture.Id(9), "Arrived", rank: "a9");
        FailChanged();

        Assert.Throws<InvalidOperationException>(() => f.Tasks.UseRepositoryFolder(f.Clone));

        Assert.Contains("Arrived", f.Titles);
    }
}
