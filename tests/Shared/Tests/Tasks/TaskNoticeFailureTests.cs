using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// After a change is saved, Tasks raises the notices the change calls for (Changed, then the settings', tag colors', and tag order's)
/// and reports the local save, each even when an earlier one failed, then throws NoticeFailedException of the task, holding every failure
/// in order, even one. A failure before or during the write is thrown as before, and so is a deletion's own failure when no notice fails.
/// A reload raises all four notices, reports no save, and throws by the platform's rule for steps that must all run.
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

    /// <summary>The notice a call raises, named as the test's log records it.</summary>
    public enum Notice { Changed, Settings, TagColors, TagOrder }

    /// <summary>A call that saves a change, the notice it raises, and whether it saves a task (so its failure's Saved is that task).</summary>
    public sealed record Call(string Name, Notice Raises, bool SavesTask, Action<ITaskStore> Run);

    public static IEnumerable<TheoryDataRow<Call>> Calls() =>
    [
        Row("Add", Notice.Changed, true, s => s.Add(new TaskDraft(TasksFixture.Id(900), "Added", "", []))),
        Row("SaveEdit", Notice.Changed, true, s => s.SaveEdit(s.All.Single(t => t.Id == Open), new TaskDraft(Open, "Edited", "", ["work"]))),
        Row("Move", Notice.Changed, true, s => s.Move(Expired, null, Open)),
        Row("Resolve", Notice.Changed, true, s => s.Resolve(Open)),
        Row("Reopen", Notice.Changed, true, s => s.Reopen(Done)),
        Row("Delete", Notice.Changed, false, s => s.Delete(Open)),
        Row("DeleteExpiredTasks", Notice.Changed, false, s => s.Synchronization.DeleteExpiredTasks()),
        Row("Direct.Add", Notice.Changed, true, s => s.Direct.Add("Added", "", [])),
        Row("Direct.Update", Notice.Changed, true, s => s.Direct.Update(Open, DirectChangeChecks.SeededTime, "Edited", null, null)),
        Row("Direct.AddImage", Notice.Changed, true, s => s.Direct.AddImage(Open, TasksFixture.TinyPng(), "pic")),
        Row("SetDeletionDelayDays", Notice.Settings, false, s => s.Settings.SetDeletionDelayDays(3)),
        Row("SetAddNewTaskShortcut", Notice.Settings, false, s => s.Settings.SetAddNewTaskShortcut("Ctrl+K")),
        Row("SetManageTasksShortcut", Notice.Settings, false, s => s.Settings.SetManageTasksShortcut("Ctrl+K")),
        Row("SetCloseWindowShortcut", Notice.Settings, false, s => ShortcutSetting.CloseWindow.Set(s.Settings, "Ctrl+K")),
        Row("SetSaveShortcut", Notice.Settings, false, s => ShortcutSetting.Save.Set(s.Settings, "Ctrl+K")),
        Row("SetConfigureShortcut", Notice.Settings, false, s => ShortcutSetting.Configure.Set(s.Settings, "Ctrl+K")),
        Row("SetContactsShortcut", Notice.Settings, false, s => ShortcutSetting.Contacts.Set(s.Settings, "Ctrl+K")),
        Row("TagColors.SetColor", Notice.TagColors, false, s => s.TagColors.SetColor("work", "red")),
        Row("TagOrder.Move", Notice.TagOrder, false, s => s.TagOrder.Move("work", null, "home")),
    ];

    private static TheoryDataRow<Call> Row(string name, Notice raises, bool savesTask, Action<ITaskStore> run) =>
        new(new Call(name, raises, savesTask, run)) { TestDisplayName = name };

    private static InvalidOperationException Failure(string name) => new(name);

    private Exception Fail(Notice notice, Exception? failure = null)
    {
        failure ??= Failure(notice.ToString());
        switch (notice)
        {
            case Notice.Changed: f.Tasks.Changed += (_, _) => throw failure; break;
            case Notice.Settings: f.Store.Settings.Changed += (_, _) => throw failure; break;
            case Notice.TagColors: f.Store.TagColors.Changed += (_, _) => throw failure; break;
            default: f.Store.TagOrder.Changed += (_, _) => throw failure; break;
        }
        return failure;
    }

    private Exception FailChanged() => Fail(Notice.Changed);

    private Exception FailSettings() => Fail(Notice.Settings);

    private Exception FailColors() => Fail(Notice.TagColors);

    private Exception FailOrder() => Fail(Notice.TagOrder);

    private Exception FailReport(Exception? failure = null)
    {
        failure ??= Failure("report");
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

    [Theory]
    [MemberData(nameof(Calls))]
    public void Throws_NoticeFailedException_of_the_task_holding_the_one_failure_when_a_notice_handler_fails(Call call)
    {
        var failure = Fail(call.Raises, new IOException("handler"));

        var thrown = Assert.Throws<NoticeFailedException<TaskItem>>(() => call.Run(f.Store));

        Assert.Equal([failure], thrown.InnerExceptions);
        Assert.Same(failure, thrown.InnerException);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Throws_NoticeFailedException_of_the_task_holding_the_one_failure_when_the_local_save_report_fails(Call call)
    {
        var failure = FailReport(new InvalidOperationException("report"));

        var thrown = Assert.Throws<NoticeFailedException<TaskItem>>(() => call.Run(f.Store));

        Assert.Equal([failure], thrown.InnerExceptions);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Throws_NoticeFailedException_of_the_task_holding_the_notice_s_failure_then_the_report_s_when_both_fail(Call call)
    {
        var notice = Fail(call.Raises, new IOException("handler"));
        var report = FailReport();

        var thrown = Assert.Throws<NoticeFailedException<TaskItem>>(() => call.Run(f.Store));

        Assert.Equal([notice, report], thrown.InnerExceptions);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Still_raises_the_notice_and_reports_the_local_save_when_the_notice_fails(Call call)
    {
        Fail(call.Raises);

        Assert.Throws<NoticeFailedException<TaskItem>>(() => call.Run(f.Store));

        Assert.Equal([call.Raises.ToString(), "LocalSaved"], log);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Carries_the_task_as_saved_for_a_call_that_saves_one_and_null_for_the_others(Call call)
    {
        FailReport();

        var thrown = Assert.Throws<NoticeFailedException<TaskItem>>(() => call.Run(f.Store));

        if (call.SavesTask)
        {
            var saved = Assert.IsType<TaskItem>(thrown.Saved);
            TaskAssert.SameTask(saved, Assert.Single(f.Tasks.All, task => task.Id == saved.Id));
        }
        else
        {
            Assert.Null(thrown.Saved);
        }
    }

    [Fact]
    public void Keeps_a_change_saved_and_visible_when_a_notice_fails()
    {
        var draft = f.Draft("Added");
        FailChanged();

        Assert.Throws<NoticeFailedException<TaskItem>>(() => f.Tasks.Add(draft));

        Assert.Contains("Added", f.Titles);
        Assert.True(f.Files.FileExists(f.TaskFile(draft.TaskId)));
    }

    [Fact]
    public void Skips_the_later_handlers_of_the_failing_event_but_still_reports_the_local_save()
    {
        FailChanged();
        f.Tasks.Changed += (_, _) => log.Add("Changed again");

        Assert.Throws<NoticeFailedException<TaskItem>>(() => f.Tasks.Resolve(Open));

        Assert.Equal(["Changed", "LocalSaved"], log);
    }

    [Fact]
    public void Holds_only_the_first_failure_of_an_event_s_handlers()
    {
        var first = FailChanged();
        f.Tasks.Changed += (_, _) => throw Failure("second handler");

        var thrown = Assert.Throws<NoticeFailedException<TaskItem>>(() => f.Tasks.Resolve(Open));

        Assert.Equal([first], thrown.InnerExceptions);
    }

    [Fact]
    public void Keeps_the_setting_when_a_settings_handler_fails()
    {
        FailSettings();

        Assert.Throws<NoticeFailedException<TaskItem>>(() => f.Store.Settings.SetDeletionDelayDays(3));

        Assert.Equal(3, f.Store.Settings.DeletionDelayDays);
    }

    [Fact]
    public void Keeps_the_shortcut_when_the_settings_handler_and_the_report_both_fail()
    {
        var settings = FailSettings();
        var report = FailReport();

        var thrown = Assert.Throws<NoticeFailedException<TaskItem>>(() => f.Store.Settings.SetSaveShortcut("Ctrl+K"));

        Assert.Equal([settings, report], thrown.InnerExceptions);
        Assert.Equal("Ctrl+K", f.Store.Settings.SaveShortcut);
    }

    [Fact]
    public void Keeps_the_color_when_a_tag_colors_handler_fails()
    {
        FailColors();

        Assert.Throws<NoticeFailedException<TaskItem>>(() => f.Store.TagColors.SetColor("work", "red"));

        Assert.Equal("red", f.Store.TagColors.ColorOf("work"));
    }

    [Fact]
    public void Keeps_the_new_order_when_a_tag_order_handler_fails()
    {
        FailOrder();

        Assert.Throws<NoticeFailedException<TaskItem>>(() => f.Store.TagOrder.Move("work", null, "home"));

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

    [Fact]
    public void Throws_the_write_s_failure_as_it_is_when_the_file_cannot_be_written_even_though_notice_handlers_would_fail()
    {
        FailChanged();
        FailReport();
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.Throws<IOException>(() => f.Tasks.Resolve(Open));

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
