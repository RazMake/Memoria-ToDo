using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// Pictures pasted into an open edit of an existing task: kept outside the clone and apart from the draft's pictures, so
/// keeping, discarding, or adding the draft never deletes them, and <see cref="ITasks.DiscardEditImages"/> deletes only them.
/// The tests find an edit's files by what appears in the device folder when one is saved, never by a folder layout.
/// </summary>
[Unit]
public sealed class EditImageTests
{
    private readonly TasksFixture f = new();
    private readonly string editedId = TasksFixture.Id(7);

    public EditImageTests() => f.SeedAndUse(editedId, "Existing");

    private static byte[] SomePng() => FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200);

    /// <summary>The files in the device folder now, by path.</summary>
    private HashSet<string> DeviceFiles() => f.Files.AllFilesUnder(f.Device).ToHashSet();

    /// <summary>Saves an image through <see cref="ITasks.SaveImage"/> and returns its Markdown and the device files it added.</summary>
    private (string Markdown, IReadOnlyList<string> Files) Save(string taskId, byte[]? image = null)
    {
        var before = DeviceFiles();
        var markdown = f.Tasks.SaveImage(taskId, image ?? SomePng(), "pic");
        return (markdown, DeviceFiles().Except(before).ToList());
    }

    private IReadOnlyList<string> SaveEditImage() => Save(editedId).Files;

    private void AssertKept(IReadOnlyList<string> files)
    {
        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.True(f.Files.FileExists(file), file));
    }

    private TaskDraft DraftWithImage()
    {
        var draft = f.Draft("A draft");
        f.Tasks.SaveImage(draft.TaskId, SomePng(), "pic");
        f.Tasks.KeepDraft(draft);
        return draft;
    }

    [Fact]
    public void Returns_markdown_for_an_existing_task_pointing_at_its_own_images_folder()
    {
        var (markdown, _) = Save(editedId);

        Assert.StartsWith($"![pic](images/{editedId}/", markdown);
    }

    [Fact]
    public void Keeps_an_open_edits_image_bytes_on_this_device()
    {
        var png = FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 300);

        var (_, files) = Save(editedId, png);

        Assert.Equal(png, f.Files.ReadFile(Assert.Single(files)));
    }

    [Fact]
    public void Writes_nothing_into_the_clone_until_the_edit_is_saved()
    {
        var cloneBefore = f.Files.AllFilesUnder(f.Clone);

        Save(editedId);

        Assert.Equal(cloneBefore, f.Files.AllFilesUnder(f.Clone));
    }

    [Fact]
    public void Discarding_edit_images_deletes_the_open_edits_images()
    {
        var first = SaveEditImage();
        var second = SaveEditImage();

        f.Tasks.DiscardEditImages(editedId);

        Assert.All(first.Concat(second), file => Assert.False(f.Files.FileExists(file), file));
    }

    [Fact]
    public void Discarding_edit_images_keeps_the_kept_draft_and_its_images()
    {
        var draft = DraftWithImage();
        SaveEditImage();

        f.Tasks.DiscardEditImages(editedId);

        Assert.Equal(draft.Title, f.Tasks.LoadDraft()!.Title);
        Assert.Single(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
    }

    [Fact]
    public void Discarding_edit_images_of_one_task_keeps_another_tasks_edit_images()
    {
        var other = TasksFixture.Id(8);
        f.Seed(other, "Other");
        f.Tasks.UseRepositoryFolder(f.Clone);
        SaveEditImage();
        var othersFiles = Save(other).Files;

        f.Tasks.DiscardEditImages(editedId);

        AssertKept(othersFiles);
    }

    [Fact]
    public void Discarding_edit_images_when_there_are_none_changes_nothing()
    {
        DraftWithImage();
        var before = DeviceFiles();

        f.Tasks.DiscardEditImages(editedId);

        Assert.Equal(before, DeviceFiles());
    }

    [Fact]
    public void Discarding_edit_images_refuses_an_invalid_task_id()
    {
        Assert.ThrowsAny<ArgumentException>(() => f.Tasks.DiscardEditImages("../evil"));
    }

    [Fact]
    public void Discarding_edit_images_for_a_draft_id_keeps_the_drafts_images()
    {
        var draft = DraftWithImage();

        f.Tasks.DiscardEditImages(draft.TaskId);

        Assert.Single(f.Files.ListFiles(f.DraftImages(draft.TaskId)));
    }

    [Fact]
    public void Discarding_the_draft_keeps_an_open_edits_images()
    {
        DraftWithImage();
        var edit = SaveEditImage();

        f.Tasks.DiscardDraft();

        AssertKept(edit);
    }

    [Fact]
    public void Keeping_a_replacing_draft_keeps_an_open_edits_images()
    {
        DraftWithImage();
        var edit = SaveEditImage();

        f.Tasks.KeepDraft(f.Draft("Replacement"));

        AssertKept(edit);
    }

    [Fact]
    public void Keeping_a_blank_draft_keeps_an_open_edits_images()
    {
        DraftWithImage();
        var edit = SaveEditImage();

        f.Tasks.KeepDraft(f.Draft(title: "", description: " "));

        AssertKept(edit);
    }

    [Fact]
    public void Adding_a_task_keeps_an_open_edits_images_out_of_the_clone()
    {
        var draft = f.Draft("New task");
        f.Tasks.SaveImage(draft.TaskId, SomePng(), "pic");
        var edit = SaveEditImage();

        f.Tasks.Add(draft);

        AssertKept(edit);
        Assert.Empty(f.Files.AllFilesUnder(f.CloneImages(editedId)));
    }

    [Fact]
    public void Saves_no_edit_image_when_the_bytes_are_not_an_image()
    {
        var before = DeviceFiles();

        Assert.Throws<InvalidDataException>(() => f.Tasks.SaveImage(editedId, [1, 2, 3], "pic"));

        Assert.Equal(before, DeviceFiles());
    }

    [Fact]
    public void Saves_no_edit_image_when_it_cannot_be_shrunk_to_1_MB()
    {
        f.Encoder.OutputSize = (_, _, _, _) => 2_000_000;
        var before = DeviceFiles();

        Assert.Throws<ImageTooLargeException>(() =>
            f.Tasks.SaveImage(editedId, FakeImageEncoder.Image(ImageFormat.Png, 4000, 3000, 5_000_000), "pic"));

        Assert.Equal(before, DeviceFiles());
    }
}
