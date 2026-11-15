using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Keyboard stops and focus rectangles: Tab goes to the rows and the buttons, never to the split bar or the list itself.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListFocusTests
{
    private static ListServices OneTask()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        return services;
    }

    [WpfFact]
    public void The_split_bar_is_not_a_keyboard_stop()
    {
        using var screen = new TaskListScreen(OneTask());

        Assert.False(screen.SplitterIsTabStop);
    }

    [WpfFact]
    public void The_list_itself_is_not_a_keyboard_stop_but_its_rows_are()
    {
        using var screen = new TaskListScreen(OneTask());

        Assert.False(screen.ListContainerIsTabStop);
        Assert.True(screen.RowIsTabStop("Buy milk"));
    }

    [WpfFact]
    public void A_row_draws_no_dashed_focus_rectangle()
    {
        using var screen = new TaskListScreen(OneTask());

        Assert.True(screen.RowHasNoFocusRectangle("Buy milk"));
    }

    [WpfFact]
    public void Tab_goes_from_the_search_to_the_rows_and_the_Sync_button_comes_after_the_list()
    {
        using var screen = new TaskListScreen(OneTask());

        var order = screen.TabOrder.ToList();

        Assert.Equal("Search", order[0]);
        Assert.Equal("Sync", order[^1]);
        Assert.True(order.IndexOf("Buy milk") < order.IndexOf("Sync"));
    }
}
