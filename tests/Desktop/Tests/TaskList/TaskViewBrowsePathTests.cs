using System.Windows.Input;
using Memoria.ToDo.Desktop.KeepAwake;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 1, opening Task View and moving around it: the window is active and titled, the search box has the focus, the groups are
/// alphabetical with No tag last, a click selects a task and shows its description, the arrow keys go between the search box and
/// the rows, a group collapses and expands, and closing and showing the window again starts it fresh. The status bar's sun shows the Prevent screen saver state and follows it, and the window reopens where
/// the user left it.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewBrowsePathTests
{
    [WpfFact]
    public void Opening_the_window_browsing_the_groups_and_rows_and_showing_it_again_starts_it_fresh()
    {
        var services = new ListServices();
        services.KeepAwake.State = new KeepAwakeState(true, false, null);
        services.Add("Plain");
        services.Add("Both", "", "work", "errand");
        services.Add("Buy milk", "Remember the oat milk", "errand");
        using var screen = new TaskListScreen(services);

        Assert.True(screen.Window.IsActive);
        Assert.Equal("Task View", screen.Window.Title);
        Assert.True(screen.SearchHasFocus);
        Assert.Equal(["errand", "work", "No tag"], screen.GroupNames);
        Assert.Equal(["Buy milk"], screen.RowTitlesIn("errand"));
        Assert.Equal(["Both"], screen.RowTitlesIn("work"));

        screen.Press(Key.Down);
        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.True(screen.RowIsKeyboardTarget("Buy milk"));
        screen.PressOn("Buy milk", Key.Up);
        Assert.True(screen.SearchHasFocus);
        screen.Press(Key.Up);
        Assert.Equal(["Plain"], screen.SelectedTitles);
        screen.PressOn("Plain", Key.Down);
        Assert.True(screen.SearchHasFocus);

        screen.ClickRow("Buy milk");
        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.Contains("Remember the oat milk", screen.Web.Page);
        screen.ClickRow("Both", "work");
        Assert.Equal(["Both"], screen.SelectedTitles);
        Assert.Equal(1, screen.SelectedRowCount);

        screen.ClickRow("Buy milk");
        screen.ToggleGroup("errand");
        Assert.DoesNotContain("Buy milk", screen.RowTitles);
        Assert.Contains("Remember the oat milk", screen.Web.Page);
        screen.ToggleGroup("errand");
        Assert.Equal(["Buy milk"], screen.SelectedTitles);

        screen.ToggleGroup("work");
        var icon = KeepAwakeIcon.In(screen.Window)!;
        Assert.Equal("Prevent screen saver is on: the screen stays on.", KeepAwakeIcon.ToolTipOf(icon));
        services.KeepAwake.Publish(new KeepAwakeState(true, false, "Access is denied."));
        Assert.StartsWith("Prevent screen saver isn't working:", KeepAwakeIcon.ToolTipOf(icon));

        screen.Window.Left -= 40;
        var movedLeft = screen.Window.Left;
        screen.Window.Close();
        Assert.True(services.Files.FileExists(services.PlacementFile));
        screen.View.Show();

        Assert.True(screen.View.IsOpen);
        Assert.InRange(screen.Window.Left, movedLeft - 2, movedLeft + 2);
        Assert.StartsWith("Prevent screen saver isn't working:", KeepAwakeIcon.ToolTipOf(KeepAwakeIcon.In(screen.Window)!));
        Assert.Equal(["errand", "work", "No tag"], screen.GroupNames);
        Assert.Equal(["Both"], screen.RowTitlesIn("work"));
        Assert.Empty(screen.SelectedTitles);
        Assert.Equal(2, services.WebViews.Count);
    }
}
