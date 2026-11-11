using Memoria.ToDo.Shared.Fakes;
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

    [Fact]
    public void Adds_a_tag_the_edit_added()
    {
        var started = Seeded(tags: ["work"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["work", "home"]));

        Assert.Equal(["work", "home"], saved.Tags);
    }

    [Fact]
    public void Removes_a_tag_the_edit_removed_ignoring_case()
    {
        var started = Seeded(tags: ["Work", "home"]);

        var saved = f.Tasks.SaveEdit(started, Edit(started, tags: ["HOME"]));

        Assert.Equal(["home"], saved.Tags);
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
        var bytes = FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200);
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
        f.Tasks.SaveImage(draft.TaskId, FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200), "pic");
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
}
