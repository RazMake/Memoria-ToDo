using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
public class ViewTasksWindowTests
{
    [WpfFact]
    public void Create_opens_no_window_and_makes_no_web_view()
    {
        using var windows = new OpenWindows();
        var services = new ListServices();

        var viewTasks = services.CreateWindow();

        Assert.Empty(windows.Visible());
        Assert.False(viewTasks.IsOpen);
        Assert.Empty(services.WebViews);
    }

    [WpfFact]
    public void Show_opens_the_window_visible_and_active()
    {
        using var screen = new TaskListScreen(new ListServices());

        Assert.True(screen.Window.IsActive);
        Assert.True(screen.View.IsOpen);
    }

    [WpfFact]
    public void A_second_Show_while_open_brings_the_same_window_forward()
    {
        using var screen = new TaskListScreen(new ListServices());
        var first = screen.Window;

        screen.View.Show();

        Assert.Same(first, screen.Window);
        Assert.True(first.IsActive);
    }

    [WpfFact]
    public void Is_not_open_after_the_user_closes_the_window()
    {
        using var screen = new TaskListScreen(new ListServices());

        screen.Window.Close();

        Assert.False(screen.View.IsOpen);
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
    public void Releases_the_web_view_when_the_window_closes()
    {
        var services = new ListServices();
        using var screen = new TaskListScreen(services);

        screen.Window.Close();

        Assert.True(Assert.Single(services.WebViews).IsDisposed);
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
        Assert.False(services.WebViews[1].IsDisposed);
        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
        Assert.Empty(screen.ExpandedTitles);
    }

    [WpfFact]
    public void Has_no_Sync_button_until_sync_is_built()
    {
        using var screen = new TaskListScreen(new ListServices());

        Assert.False(screen.HasButton("Sync"));
    }
}
