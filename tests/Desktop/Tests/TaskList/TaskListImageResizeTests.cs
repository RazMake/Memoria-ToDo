using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Dragging an image's corner in the Preview of Edit Description: the new width goes into the draft only. Nothing is saved
/// and no Undo bar shows; the Save shortcut saves it and Esc abandons it with the rest of the edit.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListImageResizeTests
{
    private const string Image = "![photo](a.png)";

    private static TaskListScreen InPreview(ListServices services)
    {
        services.Add("Buy milk", Image, "home");
        var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.ClickTab("Preview");
        return screen;
    }

    [WpfFact]
    public void Preview_shows_resize_corners_and_the_description_outside_an_edit_shows_none()
    {
        var services = new ListServices();
        services.Add("Buy milk", Image, "home");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk");
        Assert.False(screen.PageIsPreview);
        screen.PressOn("Buy milk", Key.F2);
        screen.ClickTab("Preview");

        Assert.True(screen.PageIsPreview);
    }

    [WpfFact]
    public void A_resize_in_Preview_changes_the_draft_and_saves_nothing_and_shows_no_Undo_bar()
    {
        var services = new ListServices();
        using var screen = InPreview(services);

        screen.Web.ResizeImage(0, 240);
        screen.Settle();

        Assert.Equal(Image, services.Tasks.All.Single().Description);
        Assert.False(screen.HasButton("Undo"));
        Assert.False(screen.ShowsText("Image resized"));
        screen.ClickTab("Edit");
        Assert.Equal("![photo|240](a.png)", screen.EditedDescriptionText);
    }

    [WpfFact]
    public void A_resize_does_not_show_Preview_again_and_the_next_switch_to_Preview_shows_the_new_width()
    {
        var services = new ListServices();
        using var screen = InPreview(services);
        var pages = screen.Web.Pages.Count;

        screen.Web.ResizeImage(0, 240);
        screen.Settle();
        Assert.Equal(pages, screen.Web.Pages.Count);

        screen.ClickTab("Edit");
        screen.ClickTab("Preview");

        Assert.Contains("240", screen.Web.Page);
    }

    [WpfFact]
    public void The_Save_shortcut_in_Preview_saves_the_draft_with_the_width_and_leaves_the_edit()
    {
        var services = new ListServices();
        using var screen = InPreview(services);
        screen.Web.ResizeImage(0, 240);

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Tasks.All.Single();
        Assert.Equal("![photo|240](a.png)", saved.Description);
        Assert.Equal("Buy milk", saved.Title);
        Assert.Equal(["home"], saved.Tags);
        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Esc_in_Preview_abandons_a_resize_with_the_Undo_bar_and_Undo_brings_the_draft_back()
    {
        var services = new ListServices();
        using var screen = InPreview(services);
        screen.Web.ResizeImage(0, 240);

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.Equal(Image, services.Tasks.All.Single().Description);
        screen.Click("Undo");
        Assert.Equal("![photo|240](a.png)", screen.EditedDescriptionText);
    }

    [WpfFact]
    public void A_resize_reported_while_no_edit_is_open_changes_nothing()
    {
        var services = new ListServices();
        services.Add("Buy milk", Image);
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.Web.ResizeImage(0, 240);
        screen.Settle();

        Assert.Equal(Image, services.Tasks.All.Single().Description);
        Assert.False(screen.HasButton("Undo"));
    }
}
