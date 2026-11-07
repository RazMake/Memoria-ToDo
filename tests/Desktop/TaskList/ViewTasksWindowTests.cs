using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
public class ViewTasksWindowTests
{
    [WpfFact]
    public void Create_opens_no_window()
    {
        using var windows = new OpenWindows();

        ViewTasksWindow.Create();

        Assert.Empty(windows.Visible());
    }

    [WpfFact]
    public void Show_opens_the_window_visible_and_active()
    {
        using var windows = new OpenWindows();
        var viewTasks = ViewTasksWindow.Create();

        viewTasks.Show();

        var window = Assert.Single(windows.Visible());
        Assert.True(window.IsActive);
    }

    [WpfFact]
    public void A_second_Show_while_open_brings_the_same_window_forward()
    {
        using var windows = new OpenWindows();
        var viewTasks = ViewTasksWindow.Create();
        viewTasks.Show();
        var first = Assert.Single(windows.Visible());

        viewTasks.Show();

        Assert.Same(first, Assert.Single(windows.Visible()));
        Assert.True(first.IsActive);
    }

    [WpfFact]
    public void Holds_a_one_line_notice_of_what_is_coming()
    {
        using var windows = new OpenWindows();
        ViewTasksWindow.Create().Show();
        var window = Assert.Single(windows.Visible());

        var texts = OpenWindows.TextsIn(window);

        Assert.NotEmpty(texts);
        Assert.All(texts, text => Assert.DoesNotContain('\n', text));
    }
}
