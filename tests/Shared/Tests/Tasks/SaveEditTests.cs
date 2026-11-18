using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Saving an edit of an existing task: only what the edit changed is applied, over the task as it is now.</summary>
[Unit]
public sealed class SaveEditTests
{
    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(5);

    private TaskItem Seeded(string title = "Old title", string description = "Old text", params string[] tags)
    {
        f.Seed(id, title, "a0", description, tags);
        f.Tasks.UseRepositoryFolder(f.Clone);
        return f.Tasks.All.Single();
    }

    private TaskDraft Edit(TaskItem from, string? title = null, string? description = null, string[]? tags = null) =>
        new(from.Id, title ?? from.Title, description ?? from.Description, tags ?? from.Tags);

    [Fact]
    public void Changes_the_title()
    {
        var started = Seeded();

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal("New title", saved.Title);
        Assert.Equal("New title", f.Tasks.All.Single().Title);
        Assert.Equal("Old text", saved.Description);
    }

    [Fact]
    public void Changes_the_description()
    {
        var started = Seeded();

        var saved = f.Tasks.SaveEdit(started, Edit(started, description: "line 1\r\nline 2"));

        Assert.Equal("line 1\nline 2", saved.Description);
        Assert.Equal("Old title", saved.Title);
    }

    [Fact]
    public void Trims_the_title()
    {
        var started = Seeded();

        Assert.Equal("Padded", f.Tasks.SaveEdit(started, Edit(started, title: "  Padded  ")).Title);
    }

    [Fact]
    public void Sets_updated_to_now_and_keeps_created_rank_and_resolved()
    {
        f.Seed(id, "Old title", "a7", resolved: "2026-02-02T00:00:00.000Z");
        f.Tasks.UseRepositoryFolder(f.Clone);
        var started = f.Tasks.All.Single();
        f.Time.Advance(TimeSpan.FromHours(3));

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "Changed"));

        Assert.Equal(f.Time.GetUtcNow(), saved.Updated);
        Assert.Equal(started.Created, saved.Created);
        Assert.Equal("a7", saved.Rank);
        Assert.Equal(started.Resolved, saved.Resolved);
    }

    [Fact]
    public void Writes_the_saved_task_so_a_restart_reads_it()
    {
        var started = Seeded();

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "Kept"));
        var restarted = f.Restarted();
        restarted.UseRepositoryFolder(f.Clone);

        TaskAssert.SameTask(saved, restarted.All.Single());
    }

    // The tag: one at most. The edit's tag replaces every current tag only when it differs from the started one.

    [Fact]
    public void Sets_the_tag_the_edit_chose_when_the_task_had_none()
    {
        var started = Seeded();

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["work"]));

        Assert.Equal(["work"], saved.Tags);
        Assert.Equal(["work"], f.Tasks.All.Single().Tags);
    }

    [Fact]
    public void Replaces_the_tag_with_the_one_the_edit_chose()
    {
        var started = Seeded(tags: ["work"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["home"]));

        Assert.Equal(["home"], saved.Tags);
    }

    [Fact]
    public void Clears_the_tag_when_the_edit_has_none_and_the_started_task_had_one()
    {
        var started = Seeded(tags: ["work"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: []));

        Assert.Empty(saved.Tags);
        Assert.Empty(f.Tasks.All.Single().Tags);
    }

    [Fact]
    public void Writes_the_replaced_tag_so_a_restart_reads_it()
    {
        var started = Seeded(tags: ["work"]);

        f.Tasks.SaveEdit(started, Edit(started, tags: ["home"]));
        var restarted = f.Restarted();
        restarted.UseRepositoryFolder(f.Clone);

        Assert.Equal(["home"], restarted.All.Single().Tags);
    }

    [Fact]
    public void Replaces_a_tag_another_device_set_meanwhile_when_the_edit_changed_the_tag()
    {
        var started = Seeded(tags: ["work"]);
        f.Seed(id, "Old title", "a0", "Old text", ["remote"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["home"]));

        Assert.Equal(["home"], saved.Tags);
    }

    [Fact]
    public void Keeps_a_tag_another_device_set_meanwhile_when_the_edit_kept_the_started_tag()
    {
        var started = Seeded(tags: ["work"]);
        f.Seed(id, "Old title", "a0", "Old text", ["remote"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal(["remote"], saved.Tags);
        Assert.Equal("New title", saved.Title);
    }

    [Fact]
    public void Keeps_a_tag_another_device_set_meanwhile_when_the_started_task_had_no_tag_and_the_edit_none()
    {
        var started = Seeded();
        f.Seed(id, "Old title", "a0", "Old text", ["remote"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal(["remote"], saved.Tags);
    }

    [Fact]
    public void Treats_a_tag_that_differs_only_in_case_as_the_started_tag_and_keeps_the_current_tags()
    {
        var started = Seeded(tags: ["Work"]);
        f.Seed(id, "Old title", "a0", "Old text", ["remote"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title", tags: ["WORK"]));

        Assert.Equal(["remote"], saved.Tags);
    }

    [Fact]
    public void Changes_nothing_when_the_edits_tag_differs_from_the_started_one_only_in_case()
    {
        var started = Seeded(tags: ["Work"]);
        var before = f.ReadText(f.TaskFile(id));
        var changes = f.CountChanges();

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["work"]));

        Assert.Equal(started, saved);
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Spells_the_chosen_tag_as_the_matching_tag_in_use_and_trims_it()
    {
        var started = Seeded();
        f.Seed(TasksFixture.Id(1), "Other", "a1", tags: ["Home"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["  home "]));

        Assert.Equal(["Home"], saved.Tags);
    }

    [Fact]
    public void Raises_one_notice_and_sets_updated_for_a_tag_only_change()
    {
        var started = Seeded(tags: ["work"]);
        f.Time.Advance(TimeSpan.FromHours(2));
        var changes = f.CountChanges();

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["home"]));

        Assert.Equal(1, changes.Count);
        Assert.Equal(f.Time.GetUtcNow(), saved.Updated);
    }

    [Fact]
    public void A_tag_only_edit_keeps_the_title_and_description_changed_elsewhere_meanwhile()
    {
        var started = Seeded();
        f.Seed(id, "Changed title", "a0", "Changed text");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["work"]));

        Assert.Equal("Changed title", saved.Title);
        Assert.Equal("Changed text", saved.Description);
        Assert.Equal(["work"], saved.Tags);
    }

    // A task saved with two or more tags before the one-tag rule keeps them until its tag is chosen or cleared. The
    // editor shows its first tag, so the first tag is the tag the edit started with.

    [Fact]
    public void Keeps_every_older_tag_when_the_edit_keeps_the_first_one_the_editor_showed()
    {
        var started = Seeded(tags: ["work", "home", "errand"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title", tags: ["work"]));

        Assert.Equal(["work", "home", "errand"], saved.Tags);
        Assert.Equal("New title", saved.Title);
    }

    [Fact]
    public void Keeps_every_older_tag_when_the_edit_keeps_the_first_one_in_another_case()
    {
        var started = Seeded(tags: ["Work", "home"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title", tags: ["WORK"]));

        Assert.Equal(["Work", "home"], saved.Tags);
    }

    [Fact]
    public void Replaces_every_older_tag_with_the_one_the_edit_chose()
    {
        var started = Seeded(tags: ["work", "home", "errand"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["urgent"]));

        Assert.Equal(["urgent"], saved.Tags);
    }

    [Fact]
    public void Replaces_every_older_tag_with_the_second_one_when_the_edit_chose_it()
    {
        var started = Seeded(tags: ["work", "home"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["home"]));

        Assert.Equal(["home"], saved.Tags);
    }

    [Fact]
    public void Clears_every_older_tag_when_the_edit_has_none()
    {
        var started = Seeded(tags: ["work", "home"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: []));

        Assert.Empty(saved.Tags);
    }

    [Fact]
    public void Keeps_the_older_tags_a_save_finds_in_the_current_task_when_the_edit_kept_the_started_tag()
    {
        var started = Seeded(tags: ["work"]);
        f.Seed(id, "Old title", "a0", "Old text", ["remote", "other"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal(["remote", "other"], saved.Tags);
    }

    [Fact]
    public void Refuses_an_edit_that_still_holds_all_of_a_tasks_older_tags()
    {
        var started = Seeded(tags: ["work", "home"]);
        var before = f.ReadText(f.TaskFile(id));

        Assert.Throws<ArgumentException>(() => f.Tasks.SaveEdit(started, Edit(started, title: "New title")));

        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
    }

    // Two or more tags in the edit are refused before anything changes.

    [Theory]
    [InlineData("work", "home")]
    [InlineData("work", "home", "errand")]
    public void Refuses_an_edit_with_two_or_more_tags_and_changes_nothing(params string[] tags)
    {
        var started = Seeded(tags: ["work"]);
        var before = f.ReadText(f.TaskFile(id));
        var changes = f.CountChanges();

        Assert.Throws<ArgumentException>(() => f.Tasks.SaveEdit(started, Edit(started, title: "New title", tags: tags)));

        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
        Assert.Equal("Old title", f.Tasks.All.Single().Title);
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Refuses_an_edit_with_two_tags_before_moving_the_edits_images()
    {
        var started = Seeded();
        var (markdown, _) = PasteImage();

        Assert.Throws<ArgumentException>(() => f.Tasks.SaveEdit(started, Edit(started, description: markdown, tags: ["a", "b"])));

        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.NotEmpty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Keeps_a_tag_added_elsewhere_after_the_edit_started()
    {
        var started = Seeded(tags: ["work"]);
        f.Seed(id, "Old title", "a0", "Old text", ["work", "remote"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal(["work", "remote"], saved.Tags);
        Assert.Equal("New title", saved.Title);
    }

    [Fact]
    public void Keeps_a_title_changed_elsewhere_when_the_edit_left_the_title_alone()
    {
        var started = Seeded();
        f.Seed(id, "Changed elsewhere", "a0", "Old text");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, description: "New text"));

        Assert.Equal("Changed elsewhere", saved.Title);
        Assert.Equal("New text", saved.Description);
    }

    [Fact]
    public void Keeps_a_description_changed_elsewhere_when_the_edit_left_the_description_alone()
    {
        var started = Seeded();
        f.Seed(id, "Old title", "a0", "Changed elsewhere");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal("Changed elsewhere", saved.Description);
    }

    [Fact]
    public void Keeps_a_rank_and_resolved_time_changed_elsewhere()
    {
        var started = Seeded();
        f.Seed(id, "Old title", "a9", "Old text", resolved: "2026-05-05T00:00:00.000Z");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal("a9", saved.Rank);
        Assert.NotNull(saved.Resolved);
    }

    [Fact]
    public void Changes_nothing_and_raises_no_notice_when_the_edit_changed_nothing()
    {
        var started = Seeded(tags: ["work"]);
        var before = f.ReadText(f.TaskFile(id));
        f.Time.Advance(TimeSpan.FromHours(1));
        var changes = f.CountChanges();

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: " Old title "));

        Assert.Equal(started, saved);
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Raises_one_notice_for_a_change()
    {
        var started = Seeded();
        var changes = f.CountChanges();

        f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Refuses_when_no_repository_folder_is_set()
    {
        var started = new TaskItem(id, "T", "", "a0", [], TasksFixture.Start, TasksFixture.Start, null);

        Assert.Throws<NoRepositoryFolderException>(() => f.Tasks.SaveEdit(started, Edit(started, title: "New")));
    }

    [Fact]
    public void Refuses_a_task_that_is_not_there()
    {
        Seeded();
        var other = new TaskItem(TasksFixture.Id(99), "T", "", "a0", [], TasksFixture.Start, TasksFixture.Start, null);

        Assert.Throws<TaskNotFoundException>(() => f.Tasks.SaveEdit(other, Edit(other, title: "New")));
    }

    [Fact]
    public void Refuses_an_edit_of_another_task()
    {
        var started = Seeded();

        Assert.Throws<ArgumentException>(() =>
            f.Tasks.SaveEdit(started, new TaskDraft(TasksFixture.Id(6), "New", "", [])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public void Refuses_an_empty_or_multi_line_title_and_changes_nothing(string title)
    {
        var started = Seeded();
        var before = f.ReadText(f.TaskFile(id));

        Assert.Throws<ArgumentException>(() => f.Tasks.SaveEdit(started, Edit(started, title: title)));
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public void Refuses_an_empty_or_multi_line_tag_and_changes_nothing(string tag)
    {
        var started = Seeded();
        var before = f.ReadText(f.TaskFile(id));

        Assert.Throws<ArgumentException>(() => f.Tasks.SaveEdit(started, Edit(started, title: "New", tags: [tag])));
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
    }

    // Images pasted during the edit move into the clone when it is saved.

    private (string Markdown, byte[] Bytes) PasteImage()
    {
        var bytes = TasksFixture.TinyPng();
        return (f.Tasks.SaveImage(id, bytes, "pic"), bytes);
    }

    [Fact]
    public void Moves_the_edits_images_into_the_clone()
    {
        var started = Seeded();
        var (markdown, bytes) = PasteImage();

        f.Tasks.SaveEdit(started, Edit(started, description: markdown));

        var promoted = f.Files.AllFilesUnder(f.CloneImages(id));
        Assert.Equal(bytes, f.Files.ReadFile(Assert.Single(promoted)));
    }

    [Fact]
    public void Leaves_no_edit_images_on_the_device_after_they_move()
    {
        var started = Seeded();
        PasteImage();

        f.Tasks.SaveEdit(started, Edit(started, description: "text"));

        Assert.Empty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Leaves_the_drafts_images_alone()
    {
        var started = Seeded();
        var draft = f.Draft("Other");
        f.Tasks.SaveImage(draft.TaskId, TasksFixture.TinyPng(), "pic");
        f.Tasks.KeepDraft(draft);
        var draftFiles = f.Files.AllFilesUnder(f.DraftImages(draft.TaskId));
        PasteImage();

        f.Tasks.SaveEdit(started, Edit(started, title: "New"));

        Assert.NotEmpty(draftFiles);
        Assert.All(draftFiles, file => Assert.True(f.Files.FileExists(file)));
    }

    [Fact]
    public void Moves_images_before_writing_the_task_and_keeps_them_for_a_retry_when_the_write_fails()
    {
        var started = Seeded();
        var (markdown, bytes) = PasteImage();
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.ThrowsAny<Exception>(() => f.Tasks.SaveEdit(started, Edit(started, description: markdown)));

        Assert.Equal(bytes, f.Files.ReadFile(Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)))));
        Assert.Equal("Old text", f.Tasks.All.Single().Description);
    }

    [Fact]
    public void Succeeds_on_a_retry_after_a_failed_write()
    {
        var started = Seeded();
        var (markdown, _) = PasteImage();
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        Assert.ThrowsAny<Exception>(() => f.Tasks.SaveEdit(started, Edit(started, description: markdown)));
        f.Files.StopFailing();

        var saved = f.Tasks.SaveEdit(started, Edit(started, description: markdown));

        Assert.Equal(markdown, saved.Description);
        Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)));
    }

    // Any save that changes something moves the waiting images, whichever field it changed.

    [Fact]
    public void Moves_the_waiting_images_for_a_tag_only_save()
    {
        var started = Seeded();
        var (_, bytes) = PasteImage();

        f.Tasks.SaveEdit(started, Edit(started, tags: ["work"]));

        Assert.Equal(bytes, f.Files.ReadFile(Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)))));
        Assert.Empty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Moves_the_waiting_images_for_a_clearing_tag_save()
    {
        var started = Seeded(tags: ["work"]);
        var (_, bytes) = PasteImage();

        f.Tasks.SaveEdit(started, Edit(started, tags: []));

        Assert.Equal(bytes, f.Files.ReadFile(Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)))));
        Assert.Empty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Moves_the_waiting_images_for_a_title_only_save()
    {
        var started = Seeded();
        var (_, bytes) = PasteImage();

        f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal(bytes, f.Files.ReadFile(Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)))));
        Assert.Empty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Moves_the_waiting_images_before_writing_the_task_for_a_tag_only_save_that_fails_and_keeps_them_for_a_retry()
    {
        var started = Seeded();
        var (_, bytes) = PasteImage();
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.ThrowsAny<Exception>(() => f.Tasks.SaveEdit(started, Edit(started, tags: ["work"])));

        Assert.Equal(bytes, f.Files.ReadFile(Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)))));
        Assert.Empty(f.Tasks.All.Single().Tags);
    }

    [Fact]
    public void Moves_the_waiting_images_when_one_save_changes_the_description_and_the_tag()
    {
        var started = Seeded();
        var (markdown, _) = PasteImage();

        var saved = f.Tasks.SaveEdit(started, Edit(started, description: markdown, tags: ["work"]));

        Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Equal(["work"], saved.Tags);
    }

    [Fact]
    public void Leaves_the_waiting_images_waiting_when_a_description_unchanged_save_changes_nothing()
    {
        var started = Seeded();
        PasteImage();
        var before = f.ReadText(f.TaskFile(id));

        f.Tasks.SaveEdit(started, Edit(started));

        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Single(f.Files.AllFilesUnder(f.Device));
    }
}
