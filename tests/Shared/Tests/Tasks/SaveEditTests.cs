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

    // The tags: the edit's list replaces every current tag only when it differs from the started one.

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
    public void Normalizes_the_chosen_tags_and_drops_repeats_keeping_the_first_place()
    {
        var started = Seeded();

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["  My  Home ", "WORK", "my-home", "work"]));

        Assert.Equal(["my-home", "work"], saved.Tags);
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

    // The tags are an ordered list. The edit's list replaces the current one whole only when it differs from the started one, in order.

    [Fact]
    public void Adds_a_tag_at_the_end_of_the_list()
    {
        var started = Seeded(tags: ["work", "home"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["work", "home", "errand"]));

        Assert.Equal(["work", "home", "errand"], saved.Tags);
        Assert.Equal("work", saved.GroupTag);
    }

    [Fact]
    public void Counts_a_reorder_as_a_change_and_saves_the_new_order()
    {
        var started = Seeded(tags: ["work", "home", "errand"]);
        f.Time.Advance(TimeSpan.FromHours(1));
        var changes = f.CountChanges();

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["errand", "work", "home"]));

        Assert.Equal(["errand", "work", "home"], saved.Tags);
        Assert.Equal("errand", saved.GroupTag);
        Assert.Equal(f.Time.GetUtcNow(), saved.Updated);
        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Replaces_a_tag_list_another_device_set_meanwhile_when_the_edit_reordered_the_tags()
    {
        var started = Seeded(tags: ["work", "home"]);
        f.Seed(id, "Old title", "a0", "Old text", ["work", "home", "remote"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["home", "work"]));

        Assert.Equal(["home", "work"], saved.Tags);
    }

    [Fact]
    public void Keeps_the_current_tags_when_the_edit_kept_the_started_list_in_the_same_order_after_normalizing()
    {
        var started = Seeded(tags: ["work", "home", "errand"]);
        f.Seed(id, "Old title", "a0", "Old text", ["remote", "other"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title", tags: ["WORK", " Home", "errand", "work"]));

        Assert.Equal(["remote", "other"], saved.Tags);
        Assert.Equal("New title", saved.Title);
    }

    [Fact]
    public void Replaces_the_whole_list_with_a_shorter_one()
    {
        var started = Seeded(tags: ["work", "home", "errand"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["home"]));

        Assert.Equal(["home"], saved.Tags);
    }

    [Fact]
    public void Clears_every_tag_when_the_edit_has_none()
    {
        var started = Seeded(tags: ["work", "home"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: []));

        Assert.Empty(saved.Tags);
        Assert.Null(saved.GroupTag);
    }

    [Fact]
    public void Keeps_the_current_tags_a_save_finds_when_the_edit_kept_the_started_tags()
    {
        var started = Seeded(tags: ["work"]);
        f.Seed(id, "Old title", "a0", "Old text", ["remote", "other"]);
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal(["remote", "other"], saved.Tags);
    }

    // At most 15 tags, counted after normalizing and dropping repeats. Only a new list is limited.

    private static string[] Numbered(int count) => Enumerable.Range(1, count).Select(n => $"tag{n}").ToArray();

    [Fact]
    public void Saves_fifteen_tags()
    {
        var started = Seeded();

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: Numbered(15)));

        Assert.Equal(Numbered(15), saved.Tags);
    }

    [Fact]
    public void Refuses_more_than_fifteen_tags_and_changes_nothing()
    {
        var started = Seeded(tags: ["work"]);
        var before = f.ReadText(f.TaskFile(id));
        var changes = f.CountChanges();

        Assert.Throws<ArgumentException>(() => f.Tasks.SaveEdit(started, Edit(started, title: "New title", tags: Numbered(16))));

        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
        Assert.Equal("Old title", f.Tasks.All.Single().Title);
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Counts_the_fifteen_tags_after_dropping_repeats()
    {
        var started = Seeded();

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: [.. Numbered(15), "TAG3"]));

        Assert.Equal(Numbered(15), saved.Tags);
    }

    [Fact]
    public void Saves_a_task_that_holds_more_than_fifteen_tags_keeping_all_of_them_when_the_edit_did_not_change_the_tags()
    {
        var started = Seeded(tags: Numbered(17));

        var saved = f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        Assert.Equal(Numbered(17), saved.Tags);
        Assert.Equal("New title", saved.Title);
        using var json = f.ReadJson(f.TaskFile(id));
        Assert.Equal(17, json.RootElement.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public void Saves_the_normalized_form_of_tags_read_unnormalized_on_the_next_save()
    {
        var started = Seeded(tags: ["Work", "My  Home"]);
        Assert.Equal(["work", "my-home"], started.Tags);

        f.Tasks.SaveEdit(started, Edit(started, title: "New title"));

        using var json = f.ReadJson(f.TaskFile(id));
        Assert.Equal(["work", "my-home"], json.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public void Refuses_an_edit_with_too_many_tags_before_moving_the_edits_images()
    {
        var started = Seeded();
        var (markdown, _) = PasteImage();

        Assert.Throws<ArgumentException>(() => f.Tasks.SaveEdit(started, Edit(started, description: markdown, tags: Numbered(16))));

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
    [InlineData(" \r\n ")]
    public void Refuses_a_tag_that_is_empty_once_normalized_and_changes_nothing(string tag)
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

    // A failed save undoes its image moves, so the clone is as it was.

    private const string OldImage = "old.png";

    /// <summary>Pastes <paramref name="count"/> images, and returns their paths in the edit's folder, in the order Tasks moves them, with their bytes.</summary>
    private (string Path, byte[] Bytes)[] PasteImages(int count)
    {
        for (var i = 0; i < count; i++)
            f.Tasks.SaveImage(id, TasksFixture.TinyPng(300 + i), "pic");
        var waiting = f.Files.AllFilesUnder(f.Store.EditImagesFolder);
        Assert.Equal(count, waiting.Count);
        return [.. waiting.Select(path => (path, f.Files.ReadFile(path)))];
    }

    private string CloneImage(string waitingPath) => Path.Combine(f.CloneImages(id), Path.GetFileName(waitingPath));

    private void AssertWaiting((string Path, byte[] Bytes)[] images)
    {
        Assert.Equal(images.Select(i => i.Path).Order(), f.Files.AllFilesUnder(f.Store.EditImagesFolder).Order());
        foreach (var (path, bytes) in images)
            Assert.Equal(bytes, f.Files.ReadFile(path));
    }

    private string SeedOldTaskImage()
    {
        var old = Path.Combine(f.CloneImages(id), OldImage);
        f.Files.WriteFile(old, [1, 2, 3]);
        return old;
    }

    [Fact]
    public void Moves_every_moved_image_back_and_rethrows_the_write_error_as_it_is_when_the_write_fails()
    {
        var started = Seeded();
        var old = SeedOldTaskImage();
        var images = PasteImages(2);
        var before = f.ReadText(f.TaskFile(id));
        var failure = new IOException("The disk refused the write.");
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"), failure);

        var thrown = Assert.Throws<IOException>(() => f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)")));

        Assert.Same(failure, thrown);
        AssertWaiting(images);
        Assert.Equal([old], f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Equal([1, 2, 3], f.Files.ReadFile(old));
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
        Assert.Equal("Old text", f.Tasks.All.Single().Description);
    }

    [Fact]
    public void Moves_every_moved_image_back_when_a_tag_only_save_fails_to_write()
    {
        var started = Seeded();
        var images = PasteImages(1);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.Throws<IOException>(() => f.Tasks.SaveEdit(started, Edit(started, tags: ["work"])));

        AssertWaiting(images);
        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Empty(f.Tasks.All.Single().Tags);
    }

    [Fact]
    public void Moves_the_images_before_the_failed_one_back_and_rethrows_the_moves_error_when_the_second_of_three_cannot_move()
    {
        var started = Seeded();
        var images = PasteImages(3);
        var failure = new IOException("The disk refused the second image.");
        f.Files.FailWritesUnder(CloneImage(images[1].Path), failure);
        var before = f.ReadText(f.TaskFile(id));

        var thrown = Assert.Throws<IOException>(() => f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)")));

        Assert.Same(failure, thrown);
        AssertWaiting(images);
        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
    }

    [Fact]
    public void Throws_an_aggregate_of_the_write_error_then_each_undo_error_in_order_when_moving_back_also_fails()
    {
        var started = Seeded();
        var images = PasteImages(2);
        var before = f.ReadText(f.TaskFile(id));
        var failure = new IOException("The disk refused the write.");
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"), failure);
        f.Files.FailWritesUnder(f.Device);

        var thrown = Assert.Throws<AggregateException>(() => f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)")));

        Assert.Equal(3, thrown.InnerExceptions.Count);
        Assert.All(thrown.InnerExceptions, e => Assert.IsType<IOException>(e));
        Assert.Same(failure, thrown.InnerExceptions[0]);
        Assert.EndsWith(images[1].Path, thrown.InnerExceptions[1].Message);
        Assert.EndsWith(images[0].Path, thrown.InnerExceptions[2].Message);
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
    }

    [Fact]
    public void Raises_no_change_notice_and_reports_no_local_save_when_a_save_fails()
    {
        var started = Seeded();
        PasteImages(2);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        var changes = f.CountChanges();
        var saves = f.CountLocalSaves();

        Assert.Throws<IOException>(() => f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)")));

        Assert.Equal(0, changes.Count);
        Assert.Equal(0, saves.Count);
    }

    [Fact]
    public void Raises_no_change_notice_and_reports_no_local_save_when_the_save_and_its_undo_fail()
    {
        var started = Seeded();
        PasteImages(1);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        f.Files.FailWritesUnder(f.Device);
        var changes = f.CountChanges();
        var saves = f.CountLocalSaves();

        Assert.Throws<AggregateException>(() => f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)")));

        Assert.Equal(0, changes.Count);
        Assert.Equal(0, saves.Count);
    }

    [Fact]
    public void Succeeds_on_a_retry_after_a_failed_write_and_leaves_the_images_in_the_tasks_folder()
    {
        var started = Seeded();
        var images = PasteImages(2);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        Assert.Throws<IOException>(() => f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)")));
        f.Files.StopFailing();

        var saved = f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)"));

        Assert.Equal("![pic](x)", saved.Description);
        Assert.Equal(images.Select(i => CloneImage(i.Path)).Order(), f.Files.AllFilesUnder(f.CloneImages(id)).Order());
        Assert.Empty(f.Files.AllFilesUnder(f.Store.EditImagesFolder));
    }

    [Fact]
    public void Succeeds_on_a_retry_after_a_failed_image_move_and_leaves_every_image_in_the_tasks_folder()
    {
        var started = Seeded();
        var images = PasteImages(3);
        var blocker = CloneImage(images[1].Path);
        f.Files.WriteFile(blocker, [9, 9, 9]);
        Assert.Throws<IOException>(() => f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)")));
        f.Files.DeleteFile(blocker);

        f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)"));

        Assert.Equal(images.Select(i => CloneImage(i.Path)).Order(), f.Files.AllFilesUnder(f.CloneImages(id)).Order());
        foreach (var (path, bytes) in images)
            Assert.Equal(bytes, f.Files.ReadFile(CloneImage(path)));
        Assert.Empty(f.Files.AllFilesUnder(f.Store.EditImagesFolder));
    }

    [Fact]
    public void Leaves_none_of_the_edits_images_in_the_clone_when_the_edit_is_discarded_after_a_failed_save()
    {
        var started = Seeded();
        var old = SeedOldTaskImage();
        PasteImages(2);
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        Assert.Throws<IOException>(() => f.Tasks.SaveEdit(started, Edit(started, description: "![pic](x)")));

        f.Tasks.DiscardEditImages(id);

        Assert.Equal([old], f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Empty(f.Files.AllFilesUnder(f.Store.EditImagesFolder));
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
