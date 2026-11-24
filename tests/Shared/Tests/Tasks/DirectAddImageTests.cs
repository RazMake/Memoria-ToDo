using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Adding an image to a task through <see cref="ITaskStore.Direct"/>: shrunk, kept in the clone, and linked from the description in one save.</summary>
[Unit]
public sealed class DirectAddImageTests
{
    private const int OneMegabyte = 1024 * 1024;

    private readonly TasksFixture f = new();
    private readonly string id = TasksFixture.Id(5);

    public DirectAddImageTests()
    {
        f.SeedAndUse(id, "Pictured", description: "Body");
        f.Time.Advance(TimeSpan.FromHours(2));
    }

    private IDirectTaskChanges Direct => f.Store.Direct;

    private string ImageFile() => Assert.Single(f.Files.ListFiles(f.CloneImages(id)));

    [Fact]
    public void Appends_the_images_markdown_on_a_new_line_at_the_end_of_the_description()
    {
        var saved = Direct.AddImage(id, TasksFixture.TinyPng(), "a pic");

        var lines = saved.Description.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Body", lines[0]);
        Assert.StartsWith($"![a pic](images/{id}/", lines[1]);
        Assert.EndsWith(")", lines[1]);
        TaskAssert.SameTask(saved, Assert.Single(f.Tasks.All));
    }

    [Fact]
    public void Makes_the_markdown_the_whole_description_when_the_description_is_empty()
    {
        var empty = TasksFixture.Id(6);
        f.Seed(empty, "Bare", description: "");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var saved = Direct.AddImage(empty, TasksFixture.TinyPng(), "pic");

        Assert.StartsWith($"![pic](images/{empty}/", saved.Description);
        Assert.EndsWith(")", saved.Description);
        Assert.DoesNotContain('\n', saved.Description);
    }

    [Fact]
    public void Links_the_file_it_wrote_into_the_tasks_image_folder()
    {
        var saved = Direct.AddImage(id, TasksFixture.TinyPng(), "pic");

        var fileName = Path.GetFileName(ImageFile());
        Assert.EndsWith($"](images/{id}/{fileName})", saved.Description);
    }

    [Fact]
    public void Keeps_an_image_that_already_fits_as_it_is()
    {
        var png = TasksFixture.TinyPng(300);

        Direct.AddImage(id, png, "pic");

        Assert.Equal(png, f.Files.ReadFile(ImageFile()));
    }

    [Fact]
    public void Shrinks_an_image_over_one_megabyte_before_keeping_it()
    {
        var big = FakeImageEncoder.Image(ImageFormat.Png, 4000, 3000, 3 * OneMegabyte);

        Direct.AddImage(id, big, "big");

        var kept = f.Files.ReadFile(ImageFile());
        Assert.True(kept.Length <= OneMegabyte, kept.Length.ToString());
        Assert.NotEmpty(f.Encoder.Calls);
    }

    [Fact]
    public void Sets_updated_to_now_and_raises_changed_once_with_one_local_save()
    {
        var changes = f.CountChanges();
        var saves = f.CountLocalSaves();

        var saved = Direct.AddImage(id, TasksFixture.TinyPng(), "pic");

        Assert.Equal(f.Time.GetUtcNow(), saved.Updated);
        Assert.Equal(1, changes.Count);
        Assert.Equal(1, saves.Count);
    }

    [Fact]
    public void Writes_the_description_to_the_tasks_file()
    {
        var saved = Direct.AddImage(id, TasksFixture.TinyPng(), "pic");

        using var json = f.ReadJson(f.TaskFile(id));
        Assert.Equal(saved.Description, json.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public void Adds_each_further_image_on_its_own_line_after_the_earlier_ones()
    {
        Direct.AddImage(id, TasksFixture.TinyPng(200), "one");
        var saved = Direct.AddImage(id, TasksFixture.TinyPng(400), "two");

        var lines = saved.Description.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("![one](", lines[1]);
        Assert.StartsWith("![two](", lines[2]);
        Assert.Equal(2, f.Files.ListFiles(f.CloneImages(id)).Count);
    }

    [Fact]
    public void Deletes_the_image_again_when_the_save_fails()
    {
        f.Files.FailWritesUnder(Path.Combine(f.Clone, "tasks"));
        var changes = f.CountChanges();

        Assert.Throws<IOException>(() => Direct.AddImage(id, TasksFixture.TinyPng(), "pic"));

        Assert.Empty(f.Files.AllFilesUnder(Path.Combine(f.Clone, "images")));
        Assert.Equal("Body", Assert.Single(f.Tasks.All).Description);
        Assert.Equal(0, changes.Count);
    }

    [Fact]
    public void Throws_invalid_data_for_bytes_that_are_not_an_image_and_writes_nothing()
    {
        var before = f.Snapshot();

        Assert.Throws<InvalidDataException>(() => Direct.AddImage(id, [1, 2, 3], "text"));

        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Throws_too_large_for_an_image_that_cannot_be_shrunk_to_one_megabyte_and_writes_nothing()
    {
        f.Encoder.OutputSize = (_, _, _, _) => 2 * OneMegabyte;
        var before = f.Snapshot();

        Assert.Throws<ImageTooLargeException>(() =>
            Direct.AddImage(id, FakeImageEncoder.Image(ImageFormat.Png, 4000, 3000, 3 * OneMegabyte), "huge"));

        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Throws_not_found_for_an_unknown_task_and_writes_nothing()
    {
        var before = f.Snapshot();

        Assert.Throws<TaskNotFoundException>(() => Direct.AddImage(TasksFixture.Id(99), TasksFixture.TinyPng(), "pic"));

        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Refuses_to_add_an_image_when_no_folder_is_set()
    {
        f.Tasks.UseRepositoryFolder(null);

        Assert.Throws<NoRepositoryFolderException>(() => Direct.AddImage(id, TasksFixture.TinyPng(), "pic"));
    }

    [Fact]
    public void Keeps_the_kept_draft_and_its_images_untouched()
    {
        var draft = f.Draft("Half typed", "text");
        f.Tasks.SaveImage(draft.TaskId, TasksFixture.TinyPng(), "pic");
        f.Tasks.KeepDraft(draft);
        var deviceBefore = f.DeviceSnapshot();

        Direct.AddImage(id, TasksFixture.TinyPng(), "direct");

        f.AssertDraftKept(draft);
        Assert.Equal(deviceBefore, f.DeviceSnapshot());
    }

    [Fact]
    public void Keeps_the_waiting_images_of_an_open_edit_of_the_same_task()
    {
        f.Tasks.SaveImage(id, TasksFixture.TinyPng(100), "waiting");
        var deviceBefore = f.DeviceSnapshot();

        Direct.AddImage(id, TasksFixture.TinyPng(200), "direct");

        Assert.Equal(deviceBefore, f.DeviceSnapshot());
    }
}
