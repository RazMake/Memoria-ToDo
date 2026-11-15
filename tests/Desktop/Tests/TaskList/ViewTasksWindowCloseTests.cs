using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Closing View Tasks when it isn't open. It builds no window, since the window is made only by Show, so these run
/// with no screen. Closing while it is open is in the UX tests.
/// </summary>
[Unit]
public class ViewTasksWindowCloseTests
{
    [Fact]
    public void Close_when_the_window_is_not_open_returns_true()
    {
        var viewTasks = new ListServices().CreateWindow();

        Assert.True(viewTasks.Close());
    }

    [Fact]
    public void Close_when_the_window_is_not_open_makes_no_window_web_view_or_sync()
    {
        var services = new ListServices();
        var viewTasks = services.CreateWindow();

        viewTasks.Close();

        Assert.False(viewTasks.IsOpen);
        Assert.Empty(services.WebViews);
        Assert.Equal(0, services.Sync.SyncCount);
    }
}
