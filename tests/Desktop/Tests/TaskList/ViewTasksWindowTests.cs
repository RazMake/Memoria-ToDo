using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
[Collection(ScreenTests.Collection)]
public class ViewTasksWindowTests
{
    [WpfFact]
    public void Show_opens_the_window_visible_and_active()
    {
        using var screen = new TaskListScreen(new ListServices());

        Assert.True(screen.Window.IsActive);
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void Is_titled_Task_View()
    {
        using var screen = new TaskListScreen(new ListServices());

        Assert.Equal("Task View", screen.Window.Title);
    }

    [WpfFact]
    public void Makes_one_web_view_when_it_opens_and_none_more_while_tasks_are_selected()
    {
        var services = new ListServices();
        services.Add("First");
        services.Add("Second");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("First");
        screen.ClickRow("Second");

        Assert.Single(services.WebViews);
    }

    [WpfFact]
    public void Shows_again_after_closing_with_a_new_web_view_and_nothing_selected_or_collapsed()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Call Ana");
        screen.ToggleGroup("work");
        screen.Window.Close();

        screen.View.Show();

        Assert.True(screen.View.IsOpen);
        Assert.Equal(2, services.WebViews.Count);
        Assert.True(services.WebViews[0].IsDisposed);
        Assert.False(services.WebViews[1].IsDisposed);
        Assert.Equal(["home", "work"], screen.GroupNames);
        Assert.Equal(["Call Ana"], screen.RowTitlesIn("work"));
        Assert.Empty(screen.SelectedTitles);
    }

    [WpfFact]
    public void Opens_cleanly_after_an_opening_failed_because_the_web_view_could_not_be_made()
    {
        using var windows = new OpenWindows();
        var services = new ListServices();
        var fail = true;
        var viewTasks = CreateWindow(services, services.Sync, () =>
            fail ? throw new InvalidOperationException("No web view") : new FakeDescriptionWebView());
        Assert.Throws<InvalidOperationException>(viewTasks.Show);
        fail = false;

        viewTasks.Show();

        Assert.True(viewTasks.IsOpen);
        Assert.Equal(1, services.Sync.StatusSubscriberCount);
        Assert.Equal(1, services.Sync.SyncCount);
    }

    private static IViewTasksWindow CreateWindow(ListServices services, ISyncService sync, Func<IDescriptionWebView> createWebView) =>
        ViewTasksWindow.Create(services.Tasks, services.TagColors, services.TagOrder, Palette.Colors, services.Theme, services.Time, services.Clipboard,
            new FakeImageFileChooser(), services.Files, createWebView, services.Browser, sync, services.CloseWindowKeys, services.Updates, services.SaveKeys, services.Pointer);
}
