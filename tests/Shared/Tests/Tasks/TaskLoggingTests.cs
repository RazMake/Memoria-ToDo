using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// What Tasks logs: every operation that changes tasks or settings, and every failure it ends. Entries are checked by level
/// and the facts their message names (IDs, titles, values, paths), never by wording. Descriptions are never logged.
/// </summary>
[Unit]
public sealed class TaskLoggingTests
{
    private const string Secret = "SECRET-DESCRIPTION-TEXT";
    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(1);

    private IReadOnlyList<LogEvent> During(Action operation)
    {
        var before = f.Logs.Events.Count;
        operation();
        return f.Logs.After(before);
    }

    private TaskItem Started() => f.Tasks.All.Single(t => t.Id == id);

    // Operations on tasks

    [Fact]
    public void Logs_adding_a_task_naming_its_id_and_title()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Buy milk");

        var entries = During(() => f.Tasks.Add(draft));

        entries.SingleAt(LogEventLevel.Information, draft.TaskId, "Buy milk");
    }

    [Fact]
    public void Logs_saving_an_edit_naming_the_task_id_and_new_title()
    {
        f.SeedAndUse(id, "Old title");
        var started = Started();

        var entries = During(() => f.Tasks.SaveEdit(started, new TaskDraft(id, "Brand new title", "", [])));

        entries.SingleAt(LogEventLevel.Information, id, "Brand new title");
    }

    [Fact]
    public void Logs_moving_a_task_naming_its_id_and_title()
    {
        f.Seed(id, "First", rank: "a0");
        f.Seed(TasksFixture.Id(2), "Second", rank: "a1");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var entries = During(() => f.Tasks.Move(TasksFixture.Id(2), null, id));

        entries.SingleAt(LogEventLevel.Information, TasksFixture.Id(2), "Second");
    }

    [Fact]
    public void Logs_resolving_a_task_naming_its_id_and_title()
    {
        f.SeedAndUse(id, "Finish me");

        var entries = During(() => f.Tasks.Resolve(id));

        entries.SingleAt(LogEventLevel.Information, id, "Finish me");
    }

    [Fact]
    public void Logs_reopening_a_task_naming_its_id_and_title()
    {
        f.SeedAndUse(id, "Again", resolved: "2026-11-01T00:00:00.000Z");

        var entries = During(() => f.Tasks.Reopen(id));

        entries.SingleAt(LogEventLevel.Information, id, "Again");
    }

    [Fact]
    public void Logs_deleting_a_task_naming_its_id_and_title()
    {
        f.SeedAndUse(id, "Remove me");

        var entries = During(() => f.Tasks.Delete(id));

        entries.SingleAt(LogEventLevel.Information, id, "Remove me");
    }

    [Fact]
    public void Logs_deleting_an_expired_resolved_task_naming_its_id_and_title()
    {
        f.SeedAndUse(id, "Long done", resolved: "2020-01-01T00:00:00.000Z");

        var entries = During(f.Store.Synchronization.DeleteExpiredTasks);

        entries.SingleAt(LogEventLevel.Information, id, "Long done");
    }

    [Fact]
    public void Logs_keeping_a_draft_naming_its_task_id_and_title()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("Half written");

        var entries = During(() => f.Tasks.KeepDraft(draft));

        entries.SingleAt(LogEventLevel.Information, draft.TaskId, "Half written");
    }

    [Fact]
    public void Logs_discarding_a_draft()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Tasks.KeepDraft(f.Draft("Half written"));

        var entries = During(f.Tasks.DiscardDraft);

        Assert.Single(entries, e => e.Level == LogEventLevel.Information);
    }

    [Fact]
    public void Logs_saving_an_image_naming_the_task_id()
    {
        f.SeedAndUse(id, "With picture");

        var entries = During(() => f.Tasks.SaveImage(id, TasksFixture.TinyPng(), "pic"));

        entries.SingleAt(LogEventLevel.Information, id);
    }

    // Settings, tag colors, and tag order

    [Fact]
    public void Logs_a_changed_deletion_delay_naming_the_old_and_new_values()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var entries = During(() => f.Store.Settings.SetDeletionDelayDays(30));

        entries.SingleAt(LogEventLevel.Information, "7", "30");
    }

    [Fact]
    public void Logs_a_changed_shortcut_naming_the_old_and_new_values()
    {
        f.SeedSettings(add: "Ctrl+Old");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var entries = During(() => f.Store.Settings.SetAddNewTaskShortcut("Win+Shift+New"));

        entries.SingleAt(LogEventLevel.Information, "Ctrl+Old", "Win+Shift+New");
    }

    [Fact]
    public void Logs_a_changed_tag_color_naming_the_tag_and_the_old_and_new_colors()
    {
        f.SeedTagColors(1, ("work", "teal", "2026-01-01T00:00:00.000Z"));
        f.Tasks.UseRepositoryFolder(f.Clone);

        var entries = During(() => f.Store.TagColors.SetColor("work", "amber"));

        entries.SingleAt(LogEventLevel.Information, "work", "teal", "amber");
    }

    [Fact]
    public void Logs_moving_a_tag_naming_the_tag()
    {
        f.SeedTagsAndUse(["alpha"], ["beta"], ["gamma"]);

        var entries = During(() => f.Store.TagOrder.Move("gamma", null, "alpha"));

        entries.SingleAt(LogEventLevel.Information, "gamma");
    }

    [Fact]
    public void Logs_no_information_for_a_setting_set_to_the_value_it_already_has()
    {
        f.SeedSettings(days: 7);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var entries = During(() => f.Store.Settings.SetDeletionDelayDays(7));

        Assert.DoesNotContain(entries, e => e.Level == LogEventLevel.Information);
    }

    // Failures and expected outcomes

    [Fact]
    public void Logs_no_operation_and_no_problem_for_an_add_that_is_refused()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var draft = f.Draft("");

        var entries = During(() => Assert.ThrowsAny<ArgumentException>(() => f.Tasks.Add(draft)));

        Assert.Empty(entries);
    }

    [Fact]
    public void Logs_one_warning_naming_the_file_and_the_reason_for_a_task_file_that_cannot_be_read()
    {
        f.WriteText(f.TaskFile(id), "{ this is not json");

        var entries = During(() => f.Tasks.UseRepositoryFolder(f.Clone));

        var reason = f.Repository.SkippedFiles.Single().Reason;
        entries.SingleAt(LogEventLevel.Warning, id + ".json", reason);
        Assert.Single(entries, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void Logs_one_warning_for_a_task_file_named_for_a_different_id()
    {
        f.Seed(id, "Misnamed");
        f.Files.WriteFile(f.TaskFile(TasksFixture.Id(2)), f.Files.ReadFile(f.TaskFile(id)));

        var entries = During(() => f.Tasks.UseRepositoryFolder(f.Clone));

        var reason = f.Repository.SkippedFiles.Single().Reason;
        entries.SingleAt(LogEventLevel.Warning, TasksFixture.Id(2) + ".json", reason);
        Assert.Single(entries, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void Logs_the_failure_when_keeping_a_draft_replaces_one_that_cannot_be_read()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.WriteText(f.DraftFile, "{ this is not json");

        var entries = During(() => f.Tasks.KeepDraft(f.Draft("Fresh")));

        var problem = Assert.Single(entries, e => e.Level >= LogEventLevel.Warning);
        Assert.IsType<InvalidDataException>(problem.Exception);
    }

    [Fact]
    public void Logs_no_problem_for_a_draft_that_cannot_be_read_because_the_caller_gets_the_failure()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.WriteText(f.DraftFile, "{ this is not json");

        var entries = During(() => Assert.Throws<InvalidDataException>(() => f.Tasks.LoadDraft()));

        entries.AssertNoProblems();
    }

    [Fact]
    public void Logs_no_problem_for_a_clone_without_tasks_settings_tags_or_a_draft()
    {
        var entries = During(() =>
        {
            f.Tasks.UseRepositoryFolder(f.Clone);
            _ = f.Tasks.LoadDraft();
            _ = f.Store.Settings.DeletionDelayDays;
            _ = f.Tasks.TagsInUse();
        });

        entries.AssertNoProblems();
    }

    // Descriptions

    [Fact]
    public void Never_logs_a_description_for_any_operation_or_failure()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var added = f.Tasks.Add(f.Draft("Titled", Secret));
        f.Tasks.SaveEdit(added, new TaskDraft(added.Id, "Retitled", Secret + " edited", []));
        f.Tasks.KeepDraft(f.Draft("Kept", Secret));
        f.WriteText(f.TaskFile(TasksFixture.Id(90)), "{ \"description\": \"" + Secret + "\", broken");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.DoesNotContain(f.Logs.Events, e => e.EverythingIn().Contains(Secret, StringComparison.Ordinal));
    }
}
