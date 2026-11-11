using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// An edit's pasted images stay while the user can still Undo the discard, and go when the Undo bar goes away, another
/// Undo bar replaces it, or View Tasks closes. They never touch the draft's images. The tests find an image by the
/// files the paste adds to the fake file system, so they don't depend on where Tasks keeps them.
/// </summary>
[UX]
public class TaskListEditImageTests
{
    private static readonly TimeSpan BarExpires = TimeSpan.FromSeconds(15);

    private static byte[] Png() => FakeImageEncoder.Image(ImageFormat.Png, 100, 80);

    // Opens the task's editor, pastes an image, and returns the files the paste added.
    private static IReadOnlyList<string> EditAndPasteImage(ListServices services, TaskListScreen screen, string title)
    {
        screen.PressOn(title, Key.F2);
        var before = services.AllFiles;
        services.Clipboard.Image = Png();
        screen.PasteImageIntoDescription();
        var added = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(added);
        return added;
    }

    private static bool AnyKept(ListServices services, IEnumerable<string> files) => files.Any(services.Files.FileExists);

    [WpfFact]
    public void A_discarded_edits_images_are_kept_while_the_Undo_bar_shows()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        var images = EditAndPasteImage(services, screen, "Buy milk");

        screen.Press(Key.Escape);

        Assert.True(screen.HasButton("Undo"));
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
    }

    [WpfFact]
    public async Task A_discarded_edits_images_are_deleted_when_the_Undo_bar_goes_away()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        var images = EditAndPasteImage(services, screen, "Buy milk");
        screen.Press(Key.Escape);

        await screen.PassTimeAsync(BarExpires);

        Assert.False(screen.HasButton("Undo"));
        Assert.False(AnyKept(services, images));
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void A_discarded_edits_images_are_deleted_when_another_Undo_bar_replaces_its_bar()
    {
        var services = new ListServices();
        services.Add("Other");
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        var images = EditAndPasteImage(services, screen, "Buy milk");
        screen.Press(Key.Escape);

        screen.PressOn("Other", Key.Delete);

        Assert.Equal(1, screen.CountButtons("Undo"));
        Assert.False(AnyKept(services, images));
    }

    [WpfFact]
    public void A_discarded_edits_images_are_deleted_when_View_Tasks_closes()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        var images = EditAndPasteImage(services, screen, "Buy milk");
        screen.Press(Key.Escape);

        screen.Window.Close();

        Assert.False(AnyKept(services, images));
    }

    [WpfFact]
    public void An_edit_still_open_when_View_Tasks_closes_has_its_images_deleted_and_leaves_the_task_unchanged()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Remember the oat milk");
        using var screen = new TaskListScreen(services);
        var images = EditAndPasteImage(services, screen, "Buy milk");

        screen.Window.Close();

        Assert.False(AnyKept(services, images));
        var task = services.Tasks.All.Single();
        Assert.Equal("Buy milk", task.Title);
        Assert.Equal("Remember the oat milk", task.Description);
    }

    [WpfFact]
    public void Undo_brings_back_the_edit_with_the_image_link_in_its_text_and_keeps_the_image()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        var images = EditAndPasteImage(services, screen, "Buy milk");
        var typed = screen.EditedDescriptionText;
        screen.Press(Key.Escape);

        screen.Click("Undo");

        Assert.Contains("![", typed);
        Assert.Equal(typed, screen.EditedDescriptionText);
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
    }

    [WpfFact]
    public void An_undone_edit_that_is_saved_keeps_its_image_with_the_task_after_the_window_closes()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        var images = EditAndPasteImage(services, screen, "Buy milk");
        var typed = screen.EditedDescriptionText;
        screen.Press(Key.Escape);
        screen.Click("Undo");

        screen.Press(Key.LeftCtrl, Key.Enter);
        screen.Window.Close();

        Assert.Equal(typed, services.Tasks.All.Single().Description);
        Assert.True(images.Count > 0);
        Assert.Contains(services.AllFiles, file => file.EndsWith(Path.GetFileName(images[0]), StringComparison.Ordinal));
    }

    [WpfFact]
    public async Task Discarding_an_edit_never_deletes_the_drafts_images()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        var draft = services.Tasks.StartDraft();
        var beforeDraftImage = services.AllFiles;
        services.Tasks.SaveImage(draft.TaskId, Png(), "image");
        var draftImages = services.AllFiles.Except(beforeDraftImage).ToList();
        Assert.NotEmpty(draftImages);
        using var screen = new TaskListScreen(services);
        EditAndPasteImage(services, screen, "Buy milk");
        screen.Press(Key.Escape);

        await screen.PassTimeAsync(BarExpires);
        screen.Window.Close();

        Assert.All(draftImages, image => Assert.True(services.Files.FileExists(image)));
    }
}
