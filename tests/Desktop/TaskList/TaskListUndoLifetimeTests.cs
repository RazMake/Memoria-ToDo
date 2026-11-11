using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What the one Undo bar keeps, and for how long, when the user does something else meanwhile: filters the list, starts
/// another edit, edits the same task again, or closes the window with images that can't be removed.
/// </summary>
[UX]
public class TaskListUndoLifetimeTests
{
    private static readonly TimeSpan BarExpires = TimeSpan.FromSeconds(15);

    private static byte[] Png() => FakeImageEncoder.Image(ImageFormat.Png, 100, 80);

    // Opens the task's editor, pastes an image and types tag text, and returns the files the paste added.
    private static IReadOnlyList<string> EditWithImageAndPendingTag(ListServices services, TaskListScreen screen, string title, string pendingTag)
    {
        screen.PressOn(title, Key.F2);
        var before = services.AllFiles;
        services.Clipboard.Image = Png();
        screen.PasteImageIntoDescription();
        var added = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(added);
        screen.TypeTags(pendingTag);
        return added;
    }

    [WpfTheory]
    [InlineData("search")]
    [InlineData("tag")]
    public void Undo_restores_the_edit_with_its_pending_tag_and_images_even_when_a_filter_hides_the_task(string filter)
    {
        var services = new ListServices();
        services.Add("Other", "", "work");
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        var images = EditWithImageAndPendingTag(services, screen, "Buy milk", "pend");
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
        Assert.Equal("pend", screen.EditedTagsText);
        Assert.Equal(typed, screen.EditedDescriptionText);
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
    }

    [WpfFact]
    public async Task Starting_another_edit_keeps_a_deleted_tasks_Undo_bar_until_it_expires()
    {
        var services = new ListServices();
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);
        screen.PressOn("A", Key.Delete);

        screen.PressOn("B", Key.F2);

        Assert.Equal(1, screen.CountButtons("Undo"));
        Assert.Equal(2, services.Tasks.All.Count);
        await screen.PassTimeAsync(BarExpires);
        Assert.DoesNotContain(services.Tasks.All, task => task.Title == "A");
        Assert.Single(services.Tasks.All);
    }

    [WpfFact]
    public async Task Starting_another_edit_keeps_a_discarded_edits_bar_and_images_until_it_expires()
    {
        var services = new ListServices();
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);
        var images = EditWithImageAndPendingTag(services, screen, "A", "pend");
        screen.Press(Key.Escape);

        screen.PressOn("B", Key.F2);

        Assert.Equal(1, screen.CountButtons("Undo"));
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
        await screen.PassTimeAsync(BarExpires);
        Assert.Equal(0, screen.CountButtons("Undo"));
        Assert.DoesNotContain(images, services.Files.FileExists);
    }

    [WpfFact]
    public void Editing_the_same_task_again_is_not_cleaned_up_by_the_earlier_edits_Undo_bar_expiring()
    {
        var services = new ListServices();
        services.Add("A");
        using var screen = new TaskListScreen(services);
        EditWithImageAndPendingTag(services, screen, "A", "first");
        screen.Press(Key.Escape);
        var secondImages = EditWithImageAndPendingTag(services, screen, "A", "second");

        screen.AdvanceTime(BarExpires);

        Assert.True(screen.IsEditing("A"));
        Assert.All(secondImages, image => Assert.True(services.Files.FileExists(image)));
    }

    [WpfFact]
    public void A_failed_image_removal_at_close_still_releases_the_web_view_once()
    {
        var services = new ListServices();
        services.Add("A");
        using var screen = new TaskListScreen(services);
        var images = EditWithImageAndPendingTag(services, screen, "A", "pend");
        screen.Press(Key.Escape);
        services.Files.FailDeletesUnder(System.IO.Path.GetDirectoryName(images[0])!);

        screen.Window.Close();

        Assert.True(Assert.Single(services.WebViews).IsDisposed);
    }
}

/// <summary>
/// An edit's images that can't be removed: the user is told, the action goes on, nothing retries, and starting a fresh
/// edit of that task removes the leftovers first, or doesn't open when it can't.
/// </summary>
[UX]
public class TaskListEditImageCleanupFailureTests
{
    private const string Message = "The edit's images couldn't be removed: ";
    private const string Reason = "Deleting is not allowed here";
    private static readonly TimeSpan BarExpires = TimeSpan.FromSeconds(15);

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
    public async Task A_failure_when_the_Undo_bar_expires_says_why_and_the_list_keeps_working()
    {
        var services = new ListServices();
        services.Add("A");
        using var screen = new TaskListScreen(services);
        var images = DiscardWithStuckImage(services, screen, "A");

        await screen.PassTimeAsync(BarExpires);

        Assert.False(screen.HasButton("Undo"));
        Assert.True(screen.ShowsText(Message));
        Assert.True(screen.ShowsText(Reason));
        Assert.Equal(["A"], screen.RowTitles);
        Assert.Contains(images, services.Files.FileExists);
    }

    [WpfFact]
    public void A_failure_when_another_Undo_bar_replaces_the_bar_says_why_and_the_replacing_action_goes_on()
    {
        var services = new ListServices();
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);
        DiscardWithStuckImage(services, screen, "A");

        screen.PressOn("B", Key.Delete);

        Assert.Equal(["A"], screen.RowTitles);
        Assert.Equal(1, screen.CountButtons("Undo"));
        Assert.True(screen.ShowsText(Message));
        Assert.True(screen.ShowsText(Reason));
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

    [WpfFact]
    public async Task Starting_a_fresh_edit_first_removes_the_images_an_earlier_edit_of_the_task_left()
    {
        var services = new ListServices();
        services.Add("A");
        using var screen = new TaskListScreen(services);
        var images = DiscardWithStuckImage(services, screen, "A");
        await screen.PassTimeAsync(BarExpires);
        Assert.Contains(images, services.Files.FileExists);
        services.Files.StopFailing();

        screen.PressOn("A", Key.F2);

        Assert.True(screen.IsEditing("A"));
        Assert.DoesNotContain(images, services.Files.FileExists);
    }

    [WpfFact]
    public async Task A_fresh_edit_does_not_open_while_the_leftovers_cannot_be_removed_and_other_tasks_still_edit()
    {
        var services = new ListServices();
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);
        DiscardWithStuckImage(services, screen, "A");
        await screen.PassTimeAsync(BarExpires);

        screen.PressOn("A", Key.F2);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.ShowsText(Message));
        Assert.True(screen.ShowsText(Reason));
        screen.PressOn("B", Key.F2);
        Assert.True(screen.IsEditing("B"));
    }

    [WpfFact]
    public void Undo_reopens_the_same_edit_with_its_images_without_cleaning_them_even_when_deleting_fails()
    {
        var services = new ListServices();
        services.Add("A");
        using var screen = new TaskListScreen(services);
        var images = DiscardWithStuckImage(services, screen, "A");

        screen.Click("Undo");

        Assert.True(screen.IsEditing("A"));
        Assert.Equal("A changed", screen.EditedTitleText);
        Assert.False(screen.ShowsText(Message));
        Assert.All(images, image => Assert.True(services.Files.FileExists(image)));
    }
}

/// <summary>The header keeps its controls reachable in the narrowest window.</summary>
[UX]
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

    [WpfFact]
    public void Lets_the_user_choose_and_clear_a_long_tag_at_the_narrowest_width()
    {
        using var screen = new TaskListScreen(FiveLongTags());
        screen.ResizeTo(Narrowest);

        screen.ClickChip("extraordinarily-long-third");
        Assert.Equal(["Task extraordinarily-long-third"], screen.RowTitles);

        screen.ClickChip("extraordinarily-long-third");
        Assert.Equal(5, screen.RowTitles.Count);
        Assert.Empty(screen.ControlsOutsideTheWindow());
    }
}
