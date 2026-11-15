using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Search: the "Search:" label, the box, and the magnifier button; only Enter or the button searches.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListSearchTests
{
    private static ListServices TwoTasks()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        return services;
    }

    [WpfFact]
    public void Shows_the_Search_label_and_a_magnifier_button_with_the_tooltip_Search_Enter()
    {
        using var screen = new TaskListScreen(TwoTasks());

        Assert.True(screen.ShowsSearchLabel);
        Assert.Equal("Search (Enter)", screen.SearchButtonTooltip);
    }

    [WpfFact]
    public void Typing_alone_does_not_search()
    {
        using var screen = new TaskListScreen(TwoTasks());

        screen.TypeSearch("milk");

        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void Enter_in_the_box_searches()
    {
        using var screen = new TaskListScreen(TwoTasks());

        screen.Search("milk");

        Assert.Equal(["Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void The_magnifier_button_searches()
    {
        using var screen = new TaskListScreen(TwoTasks());
        screen.TypeSearch("ana");

        screen.PressSearchButton();

        Assert.Equal(["Call Ana"], screen.RowTitles);
    }

    [WpfFact]
    public void Enter_in_an_empty_box_shows_every_task_again()
    {
        using var screen = new TaskListScreen(TwoTasks());
        screen.Search("milk");
        Assert.Equal(["Buy milk"], screen.RowTitles);

        screen.Search("");

        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void Ctrl_F_gives_the_search_box_the_focus()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.Select("Buy milk");
        Assert.True(screen.RowIsKeyboardTarget("Buy milk"), "Setup: the row should hold the focus before the shortcut.");

        screen.Press(Key.LeftCtrl, Key.F);

        Assert.True(screen.SearchHasFocus);
    }
}
