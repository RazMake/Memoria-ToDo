using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Search: the "Search:" label and the box, which searches as the user types; the keys that reach and leave it.</summary>
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
    public void Shows_the_Search_label()
    {
        using var screen = new TaskListScreen(TwoTasks());

        Assert.True(screen.ShowsSearchLabel);
    }

    [WpfFact]
    public void The_search_box_has_the_focus_when_the_window_opens()
    {
        using var screen = new TaskListScreen(TwoTasks());

        Assert.True(screen.SearchHasFocus);
    }

    [WpfFact]
    public void Typing_searches_at_once()
    {
        using var screen = new TaskListScreen(TwoTasks());

        screen.TypeSearch("milk");

        Assert.Equal(["Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void Emptying_the_box_shows_every_task_again()
    {
        using var screen = new TaskListScreen(TwoTasks());
        screen.TypeSearch("milk");
        Assert.Equal(["Buy milk"], screen.RowTitles);

        screen.TypeSearch("");

        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void Down_in_the_box_selects_the_top_row()
    {
        using var screen = new TaskListScreen(TwoTasks());

        screen.Press(Key.Down);

        Assert.Equal(["Call Ana"], screen.SelectedTitles);
        Assert.True(screen.RowIsKeyboardTarget("Call Ana"));
    }

    [WpfFact]
    public void Up_in_the_box_selects_the_bottom_row()
    {
        using var screen = new TaskListScreen(TwoTasks());

        screen.Press(Key.Up);

        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.True(screen.RowIsKeyboardTarget("Buy milk"));
    }

    [WpfFact]
    public void Up_in_the_box_scrolls_the_list_to_a_bottom_row_hidden_below_it()
    {
        var services = new ListServices();
        for (var i = 1; i <= 40; i++)
        {
            services.Add($"Task {i:00}");
        }

        using var screen = new TaskListScreen(services);
        var bottom = screen.RowTitles[^1];
        Assert.False(screen.RowIsScrolledIntoView(bottom), "Setup: the bottom row should start hidden below the list.");
        screen.Press(Key.LeftCtrl, Key.F);

        screen.Press(Key.Up);

        Assert.Equal([bottom], screen.SelectedTitles);
        Assert.True(screen.RowIsScrolledIntoView(bottom));
    }

    [WpfFact]
    public void Ctrl_F_in_an_open_description_edit_moves_to_the_search_box()
    {
        using var screen = new TaskListScreen(TwoTasks());
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat");

        screen.Press(Key.LeftCtrl, Key.F);

        Assert.True(screen.SearchHasFocus);
    }

    [WpfFact]
    public void Up_on_the_top_row_moves_to_the_search_box()
    {
        using var screen = new TaskListScreen(TwoTasks());

        screen.PressOn("Call Ana", Key.Up);

        Assert.True(screen.SearchHasFocus);
    }

    [WpfFact]
    public void Down_on_the_bottom_row_moves_to_the_search_box()
    {
        using var screen = new TaskListScreen(TwoTasks());

        screen.PressOn("Buy milk", Key.Down);

        Assert.True(screen.SearchHasFocus);
    }

    [WpfFact]
    public void Up_in_the_box_focuses_the_bottom_row_of_a_task_that_also_shows_in_an_earlier_group()
    {
        var services = new ListServices();
        services.Add("Shared", "", "a", "z");
        for (var i = 1; i <= 40; i++)
        {
            services.Add($"Task {i:00}", "", "m");
        }

        using var screen = new TaskListScreen(services);
        Assert.False(screen.RowIsScrolledIntoView("Shared", "z"), "Setup: the bottom row should start hidden below the list.");
        screen.Press(Key.LeftCtrl, Key.F);

        screen.Press(Key.Up);

        Assert.True(screen.RowIsKeyboardTarget("Shared", "z"));
        Assert.True(screen.RowIsScrolledIntoView("Shared", "z"));
    }

    [WpfFact]
    public void Ctrl_F_searches_even_when_it_is_the_Save_key_and_leaves_the_edit_open()
    {
        var services = TwoTasks();
        services.SaveKeys.Set(ModifierKeys.Control, Key.F);
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat");

        screen.Press(Key.LeftCtrl, Key.F);

        Assert.True(screen.SearchHasFocus);
        Assert.True(screen.IsEditingAny);
    }

    [WpfFact]
    public void Ctrl_F_in_a_rows_open_menu_closes_it_and_moves_to_the_search_box()
    {
        using var screen = new TaskListScreen(TwoTasks());
        screen.OpenContextMenu("Buy milk");

        screen.PressInContextMenu("Buy milk", Key.LeftCtrl, Key.F);

        Assert.False(screen.ContextMenuIsOpen("Buy milk"));
        Assert.True(screen.SearchHasFocus);
    }

    [WpfFact]
    public void Showing_the_open_window_again_empties_the_search_and_gives_it_the_focus()
    {
        using var screen = new TaskListScreen(TwoTasks());
        screen.TypeSearch("milk");
        screen.Select("Buy milk");

        screen.View.Show();

        Assert.Equal("", screen.SearchText);
        Assert.True(screen.SearchHasFocus);
        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void Ctrl_F_gives_the_search_box_the_focus_and_selects_its_text()
    {
        using var screen = new TaskListScreen(TwoTasks());
        screen.TypeSearch("a");
        screen.Select("Call Ana");
        Assert.True(screen.RowIsKeyboardTarget("Call Ana"), "Setup: the row should hold the focus before the shortcut.");

        screen.Press(Key.LeftCtrl, Key.F);

        Assert.True(screen.SearchHasFocus);
        Assert.Equal("a", screen.SearchSelectedText);
    }
}
