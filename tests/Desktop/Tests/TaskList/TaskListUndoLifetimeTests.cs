using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// An Undo of a discarded edit brings back what was typed and pasted, even when a filter now hides the task. The rules of
/// the bar's lifetime are unit-tested in PendingUndoTests and EditCleanupTests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListUndoLifetimeTests
{
    private static byte[] Png() => FakeImageEncoder.Image(ImageFormat.Png, 100, 80);

    // Opens the task's editor and pastes an image, and returns the files the paste added.
    private static IReadOnlyList<string> EditWithImage(ListServices services, TaskListScreen screen, string title)
    {
        screen.PressOn(title, Key.F2);
        var before = services.AllFiles;
        services.Clipboard.Image = Png();
        screen.PasteImageIntoDescription();
        var added = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(added);
        return added;
    }

    [WpfTheory]
    [InlineData("search")]
    [InlineData("tag")]
    public void Undo_restores_the_edit_with_its_images_even_when_a_filter_hides_the_task(string filter)
    {
        var services = new ListServices();
        services.Add("Other", "", "work");
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        var images = EditWithImage(services, screen, "Buy milk");
        var typed = screen.EditedDescriptionText;
        screen.Press(Key.Escape);
        if (filter == "search")
        {
            screen.Search("Other");
        }
        else
        {
            screen.ClickChip("work");
        }

        Assert.DoesNotContain("Buy milk", screen.RowTitles);
        Assert.Equal(1, screen.CountButtons("Undo"));
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
        screen.Click("Undo");

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal(typed, screen.EditedDescriptionText);
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
    }
}

/// <summary>
/// An edit's images that can't be removed as the window closes: the web view is still released, and the user is told the next time View Tasks opens.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListEditImageCleanupFailureTests
{
    private const string Message = "The edit's images couldn't be removed: ";
    private const string Reason = "Deleting is not allowed here";
    // Discards an edit of the task that has a pasted image, with deleting its folder set to fail; returns the images.
    private static IReadOnlyList<string> DiscardWithStuckImage(ListServices services, TaskListScreen screen, string title)
    {
        screen.PressOn(title, Key.F2);
        var before = services.AllFiles;
        services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        screen.PasteImageIntoDescription();
        var images = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(images);
        screen.TypeTitle(title, title + " changed");
        screen.Press(Key.Escape);
        services.Files.FailDeletesUnder(System.IO.Path.GetDirectoryName(images[0])!);
        return images;
    }

    [WpfFact]
    public void A_failure_as_the_window_closes_releases_the_web_view_and_shows_its_message_the_next_time_it_opens()
    {
        var services = new ListServices();
        services.Add("A");
        using var screen = new TaskListScreen(services);
        DiscardWithStuckImage(services, screen, "A");
        screen.Window.Close();

        screen.View.Show();

        Assert.True(services.WebViews[0].IsDisposed);
        Assert.True(screen.ShowsText(Message));
        Assert.True(screen.ShowsText(Reason));
    }
}

/// <summary>The header keeps its controls reachable in the narrowest window.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListNarrowWindowTests
{
    private const double Narrowest = 400;

    private static ListServices FiveLongTags()
    {
        var services = new ListServices();
        foreach (var tag in new[] { "extraordinarily-long-first", "extraordinarily-long-second", "extraordinarily-long-third", "extraordinarily-long-fourth", "extraordinarily-long-fifth" })
        {
            services.Add($"Task {tag}", "", tag);
        }

        return services;
    }

    [WpfFact]
    public void Keeps_the_tag_filter_and_search_inside_the_window_at_its_narrowest_with_long_tags()
    {
        using var screen = new TaskListScreen(FiveLongTags());

        screen.ResizeTo(Narrowest);

        Assert.NotEmpty(screen.ChipTags);
        Assert.Empty(screen.ControlsOutsideTheWindow());
    }
}
