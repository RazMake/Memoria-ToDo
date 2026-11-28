using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// <see cref="ITaskStore.EditImagesFolder"/>: where an open edit's images wait, laid out like the clone, so the link path
/// <see cref="ITasks.SaveImage"/> returns names the image under it.
/// </summary>
[Unit]
public sealed class EditImagesFolderTests
{
    private readonly TasksFixture f = new();
    private readonly string editedId = TasksFixture.Id(7);

    public EditImagesFolderTests() => f.SeedAndUse(editedId, "Existing");

    /// <summary>The link's path, such as <c>images/&lt;task-id&gt;/&lt;file&gt;</c>, from <c>![alt](path)</c>.</summary>
    private static string LinkPath(string markdown) => markdown[(markdown.IndexOf("](", StringComparison.Ordinal) + 2)..^1];

    private string UnderEditImages(string linkPath) => Path.Combine(f.Store.EditImagesFolder, linkPath.Replace('/', Path.DirectorySeparatorChar));

    private string UnderClone(string linkPath) => Path.Combine(f.Clone, linkPath.Replace('/', Path.DirectorySeparatorChar));

    private string SaveEditImage() => LinkPath(f.Tasks.SaveImage(editedId, TasksFixture.TinyPng(), "pic"));

    [Fact]
    public void Holds_an_open_edits_image_at_the_links_path_under_it()
    {
        var png = TasksFixture.TinyPng();

        var link = LinkPath(f.Tasks.SaveImage(editedId, png, "pic"));

        Assert.StartsWith($"images/{editedId}/", link);
        Assert.Equal(png, f.Files.ReadFile(UnderEditImages(link)));
    }

    [Fact]
    public void Does_not_hold_a_drafts_image()
    {
        var draft = f.Draft("A draft");

        var link = LinkPath(f.Tasks.SaveImage(draft.TaskId, TasksFixture.TinyPng(), "pic"));

        Assert.False(f.Files.FileExists(UnderEditImages(link)));
    }

    [Fact]
    public void Moves_the_image_into_the_clone_at_the_same_relative_path_when_the_edit_is_saved()
    {
        var started = f.Tasks.All.Single();
        var link = SaveEditImage();
        var edited = new TaskDraft(editedId, started.Title, $"![pic]({link})", started.Tags);

        f.Tasks.SaveEdit(started, edited);

        Assert.False(f.Files.FileExists(UnderEditImages(link)));
        Assert.True(f.Files.FileExists(UnderClone(link)));
    }

    [Fact]
    public void Loses_the_image_when_the_edit_images_are_discarded()
    {
        var link = SaveEditImage();
        Assert.True(f.Files.FileExists(UnderEditImages(link)));

        f.Tasks.DiscardEditImages(editedId);

        Assert.False(f.Files.FileExists(UnderEditImages(link)));
    }

    [Fact]
    public void Is_the_same_on_every_read_before_and_after_the_repository_folder_changes()
    {
        var before = f.Store.EditImagesFolder;

        var again = f.Store.EditImagesFolder;
        f.Tasks.UseRepositoryFolder(f.OtherClone);
        var afterSwitch = f.Store.EditImagesFolder;
        f.Tasks.UseRepositoryFolder(null);
        var afterNone = f.Store.EditImagesFolder;

        Assert.False(string.IsNullOrWhiteSpace(before));
        Assert.Equal(before, again);
        Assert.Equal(before, afterSwitch);
        Assert.Equal(before, afterNone);
    }
}
