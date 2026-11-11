using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Resolving, reopening, and deleting tasks, and finding resolved tasks.</summary>
[Unit]
public sealed class ResolveReopenDeleteTests
{
    private const string ResolvedAt = "2026-02-02T03:04:05.000Z";
    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(1);

    private void Use(string? resolved = null)
    {
        f.Seed(id, "Task", "a0", "text", ["work"], resolved);
        f.Tasks.UseRepositoryFolder(f.Clone);
    }

    // Resolve

    [Fact]
    public void Resolves_an_open_task_at_the_current_time()
    {
        Use();
        f.Time.Advance(TimeSpan.FromDays(1));

        var saved = f.Tasks.Resolve(id);

        Assert.Equal(f.Time.GetUtcNow(), saved.Resolved);
        Assert.Equal(f.Time.GetUtcNow(), saved.Updated);
        Assert.Equal(saved, f.Tasks.All.Single());
    }

    [Fact]
    public void Keeps_the_other_fields_of_a_resolved_task()
    {
        Use();

        var saved = f.Tasks.Resolve(id);

        Assert.Equal(("Task", "text", "a0"), (saved.Title, saved.Description, saved.Rank));
        Assert.Equal(["work"], saved.Tags);
    }

    [Fact]
    public void Keeps_a_resolved_task_resolved_after_a_restart()
    {
        Use();
        f.Tasks.Resolve(id);
        var restarted = f.Restarted();
        restarted.UseRepositoryFolder(f.Clone);

        Assert.NotNull(restarted.All.Single().Resolved);
    }

    [Fact]
    public void Leaves_an_already_resolved_task_as_it_is_and_raises_no_notice()
    {
        Use(ResolvedAt);
        var before = f.ReadText(f.TaskFile(id));
        var changes = f.CountChanges();
        f.Time.Advance(TimeSpan.FromDays(1));

        var saved = f.Tasks.Resolve(id);

        Assert.Equal(f.Tasks.All.Single(), saved);
        Assert.Equal(DateTimeOffset.Parse(ResolvedAt), saved.Resolved);
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Raises_one_notice_when_a_task_is_resolved()
    {
        Use();
        var changes = f.CountChanges();

        f.Tasks.Resolve(id);

        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Refuses_to_resolve_a_task_that_is_not_there()
    {
        Use();

        Assert.Throws<TaskNotFoundException>(() => f.Tasks.Resolve(TasksFixture.Id(99)));
    }

    // Reopen

    [Fact]
    public void Reopens_a_resolved_task_at_the_current_time()
    {
        Use(ResolvedAt);
        f.Time.Advance(TimeSpan.FromDays(1));

        var saved = f.Tasks.Reopen(id);

        Assert.Null(saved.Resolved);
        Assert.Equal(f.Time.GetUtcNow(), saved.Updated);
        Assert.Equal(saved, f.Tasks.All.Single());
    }

    [Fact]
    public void Keeps_a_reopened_task_open_after_a_restart()
    {
        Use(ResolvedAt);
        f.Tasks.Reopen(id);
        var restarted = f.Restarted();
        restarted.UseRepositoryFolder(f.Clone);

        Assert.Null(restarted.All.Single().Resolved);
    }

    [Fact]
    public void Leaves_an_open_task_as_it_is_and_raises_no_notice()
    {
        Use();
        var before = f.ReadText(f.TaskFile(id));
        var changes = f.CountChanges();
        f.Time.Advance(TimeSpan.FromDays(1));

        var saved = f.Tasks.Reopen(id);

        Assert.Equal(f.Tasks.All.Single(), saved);
        Assert.Equal(before, f.ReadText(f.TaskFile(id)));
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Raises_one_notice_when_a_task_is_reopened()
    {
        Use(ResolvedAt);
        var changes = f.CountChanges();

        f.Tasks.Reopen(id);

        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Refuses_to_reopen_a_task_that_is_not_there()
    {
        Use();

        Assert.Throws<TaskNotFoundException>(() => f.Tasks.Reopen(TasksFixture.Id(99)));
    }

    // Delete

    private byte[] PasteEditImage() => PasteImage(id);

    private byte[] PasteImage(string taskId)
    {
        var bytes = FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200);
        f.Tasks.SaveImage(taskId, bytes, "pic");
        return bytes;
    }

    private void PutCloneImage(string taskId) =>
        f.Files.WriteFile(Path.Combine(f.CloneImages(taskId), "x.png"), [1, 2, 3]);

    [Fact]
    public void Deletes_the_task_file_and_the_task()
    {
        Use();

        f.Tasks.Delete(id);

        Assert.Empty(f.Tasks.All);
        Assert.False(f.Files.FileExists(f.TaskFile(id)));
    }

    [Fact]
    public void Deletes_the_tasks_images_in_the_clone()
    {
        Use();
        PutCloneImage(id);

        f.Tasks.Delete(id);

        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
    }

    [Fact]
    public void Deletes_the_tasks_kept_edit_images()
    {
        Use();
        PasteEditImage();

        f.Tasks.Delete(id);

        Assert.Empty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Leaves_other_tasks_and_their_images_alone()
    {
        Use();
        var other = TasksFixture.Id(2);
        f.Seed(other, "Other", "a1");
        f.Tasks.UseRepositoryFolder(f.Clone);
        PutCloneImage(other);

        f.Tasks.Delete(id);

        Assert.Equal(["Other"], f.Titles);
        Assert.Single(f.Files.AllFilesUnder(f.CloneImages(other)));
    }

    [Fact]
    public void Leaves_the_drafts_images_alone()
    {
        Use();
        var draft = f.Draft("Draft");
        f.Tasks.SaveImage(draft.TaskId, FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200), "pic");
        f.Tasks.KeepDraft(draft);

        f.Tasks.Delete(id);

        Assert.NotEmpty(f.Files.AllFilesUnder(f.DraftImages(draft.TaskId)));
    }

    /// <summary>
    /// Another repository with a task of the same ID and an image of its own, and a notice handler that switches to it
    /// once the task list is empty, then runs <paramref name="whenSwitched"/>.
    /// </summary>
    private (string TaskFile, string Images, string Image) OtherRepositoryOnceEmpty(Action? whenSwitched = null)
    {
        var otherClone = Path.Combine(Path.GetDirectoryName(f.Clone)!, "other-clone");
        var taskFile = Path.Combine(otherClone, "tasks", id + ".json");
        var images = Path.Combine(otherClone, "images", id);
        var image = Path.Combine(images, "x.png");
        f.WriteText(taskFile, f.ReadText(f.TaskFile(id)).Replace("\"Task\"", "\"Other repository task\""));
        f.Files.WriteFile(image, [9, 8, 7, 6]);
        f.Tasks.Changed += (_, _) =>
        {
            if (f.Tasks.All.Count != 0)
                return;
            f.Tasks.UseRepositoryFolder(otherClone);
            whenSwitched?.Invoke();
        };
        return (taskFile, images, image);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(200)]
    public void Keeps_an_edit_image_pasted_into_the_other_repository_by_a_notice_handler_and_saves_it_there(int otherSize)
    {
        Use();
        PutCloneImage(id);
        PasteEditImage();
        var pasted = FakeImageEncoder.Image(ImageFormat.Png, 10, 10, otherSize);
        string markdown = "";
        var other = OtherRepositoryOnceEmpty(() => markdown = f.Tasks.SaveImage(id, pasted, "pic"));

        f.Tasks.Delete(id);
        var started = f.Tasks.All.Single();
        f.Tasks.SaveEdit(started, new TaskDraft(id, started.Title, markdown, started.Tags));

        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Equal(["Other repository task"], f.Titles);
        var kept = f.Files.AllFilesUnder(other.Images).Select(f.Files.ReadFile).ToList();
        Assert.Equal(2, kept.Count);
        Assert.Contains(kept, bytes => bytes.SequenceEqual(new byte[] { 9, 8, 7, 6 }));
        Assert.Contains(kept, bytes => bytes.SequenceEqual(pasted));
    }

    [Fact]
    public void Deletes_the_images_of_the_repository_it_deleted_from_when_a_notice_handler_switches_repositories()
    {
        Use();
        PutCloneImage(id);
        var (otherTaskFile, _, otherImage) = OtherRepositoryOnceEmpty();

        f.Tasks.Delete(id);

        Assert.False(f.Files.FileExists(f.TaskFile(id)));
        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.True(f.Files.FileExists(otherTaskFile));
        Assert.Equal([9, 8, 7, 6], f.Files.ReadFile(otherImage));
        Assert.Equal(["Other repository task"], f.Titles);
    }

    [Fact]
    public void Stays_deleted_after_a_restart()
    {
        Use();
        f.Tasks.Delete(id);
        var restarted = f.Restarted();
        restarted.UseRepositoryFolder(f.Clone);

        Assert.Empty(restarted.All);
    }

    [Fact]
    public void Raises_one_notice_when_a_task_is_deleted()
    {
        Use();
        var changes = f.CountChanges();

        f.Tasks.Delete(id);

        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Refuses_to_delete_a_task_that_is_not_there_and_deletes_nothing()
    {
        Use();

        Assert.Throws<TaskNotFoundException>(() => f.Tasks.Delete(TasksFixture.Id(99)));
        Assert.Single(f.Tasks.All);
    }

    [Fact]
    public void Stops_and_reports_the_failure_when_the_task_file_cannot_be_deleted_keeping_the_images()
    {
        Use();
        PutCloneImage(id);
        f.Files.FailDeletesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.ThrowsAny<Exception>(() => f.Tasks.Delete(id));

        Assert.True(f.Files.FileExists(f.TaskFile(id)));
        Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)));
    }

    [Fact]
    public void Keeps_the_task_in_the_list_and_raises_no_notice_when_the_task_file_cannot_be_deleted()
    {
        Use();
        var changes = f.CountChanges();
        f.Files.FailDeletesUnder(Path.Combine(f.Clone, "tasks"));

        Assert.ThrowsAny<Exception>(() => f.Tasks.Delete(id));

        Assert.Equal(id, f.Tasks.All.Single().Id);
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Removes_the_task_from_the_list_and_raises_a_notice_even_when_deleting_its_clone_images_fails()
    {
        Use();
        PutCloneImage(id);
        var changes = f.CountChanges();
        f.Files.FailDeletesUnder(f.CloneImages(id));

        Assert.ThrowsAny<Exception>(() => f.Tasks.Delete(id));

        Assert.Empty(f.Tasks.All);
        Assert.False(f.Files.FileExists(f.TaskFile(id)));
        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Removes_the_task_from_the_list_and_raises_a_notice_even_when_deleting_its_edit_images_fails()
    {
        Use();
        PasteEditImage();
        var changes = f.CountChanges();
        f.Files.FailDeletesUnder(f.Device);

        Assert.ThrowsAny<Exception>(() => f.Tasks.Delete(id));

        Assert.Empty(f.Tasks.All);
        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void Deletes_the_clone_images_before_trying_the_edit_images()
    {
        Use();
        PutCloneImage(id);
        PasteEditImage();
        f.Files.FailDeletesUnder(f.Device);

        Assert.ThrowsAny<Exception>(() => f.Tasks.Delete(id));

        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(id)));
    }

    [Fact]
    public void Stops_and_reports_the_failure_when_the_clone_images_cannot_be_deleted_keeping_the_edit_images()
    {
        Use();
        PutCloneImage(id);
        PasteEditImage();
        f.Files.FailDeletesUnder(f.CloneImages(id));

        Assert.ThrowsAny<Exception>(() => f.Tasks.Delete(id));

        Assert.Single(f.Files.AllFilesUnder(f.CloneImages(id)));
        Assert.Single(f.Files.AllFilesUnder(f.Device));
    }

    // Resolved tasks stay visible.

    [Fact]
    public void Lists_resolved_tasks_with_open_ones_in_rank_order()
    {
        f.Seed(TasksFixture.Id(1), "First", "a0", resolved: ResolvedAt);
        f.Seed(TasksFixture.Id(2), "Second", "a1");
        f.Seed(TasksFixture.Id(3), "Third", "a2", resolved: ResolvedAt);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["First", "Second", "Third"], f.Titles);
    }

    [Fact]
    public void Finds_resolved_tasks_by_text()
    {
        f.Seed(TasksFixture.Id(1), "Buy milk", "a0", resolved: ResolvedAt);
        f.Seed(TasksFixture.Id(2), "Buy eggs", "a1");
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Buy milk", "Buy eggs"], f.Tasks.Search("buy").Select(t => t.Title));
    }

    [Fact]
    public void Keeps_a_resolved_task_in_its_place_when_it_is_resolved()
    {
        f.Seed(TasksFixture.Id(1), "First", "a0");
        f.Seed(TasksFixture.Id(2), "Second", "a1");
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.Resolve(TasksFixture.Id(1));

        Assert.Equal(["First", "Second"], f.Titles);
    }
}
