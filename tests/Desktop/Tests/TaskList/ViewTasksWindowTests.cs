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
    public void Makes_one_web_view_when_it_opens_and_none_more_while_tasks_expand()
    {
        var services = new ListServices();
        services.Add("First");
        services.Add("Second");
        using var screen = new TaskListScreen(services);

        screen.PressOn("First", System.Windows.Input.Key.Enter);
        screen.PressOn("Second", System.Windows.Input.Key.Enter);

        Assert.Single(services.WebViews);
    }

    [WpfFact]
    public void Shows_again_after_closing_with_a_new_web_view_and_nothing_expanded_or_filtered()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Call Ana", System.Windows.Input.Key.Enter);
        screen.ClickChip("work");
        screen.Window.Close();

        screen.View.Show();

        Assert.True(screen.View.IsOpen);
        Assert.Equal(2, services.WebViews.Count);
        Assert.True(services.WebViews[0].IsDisposed);
        Assert.False(services.WebViews[1].IsDisposed);
        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
        Assert.Empty(screen.ExpandedTitles);
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
        ViewTasksWindow.Create(services.Tasks, services.TagColors, Palette.Colors, services.Theme, services.Time, services.Clipboard,
            new FakeImageFileChooser(), services.Files, createWebView, services.Browser, sync, services.CloseWindowKeys);
}
