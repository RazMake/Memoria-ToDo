using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
[Collection(ScreenTests.Collection)]
public class TaskListReorderTests
{
    [WpfFact]
    public void Alt_Down_moves_the_task_one_place_down_and_keeps_the_new_rank()
    {
        var services = new ListServices();
        services.Add("C");
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);

        screen.PressOn("A", Key.LeftAlt, Key.Down);

        Assert.Equal(["B", "A", "C"], screen.RowTitles);
        Assert.Equal(["B", "A", "C"], services.OpenTitlesInRankOrder());
    }

}

[UX]
[Collection(ScreenTests.Collection)]
public class TaskListResolveTests
{
    [WpfFact]
    public void Ticking_the_checkbox_resolves_the_task_at_once_and_counts_it_as_resolved()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);

        screen.ClickCheckBox("Buy milk");

        Assert.Equal(["Call Ana"], screen.RowTitles);
        Assert.NotNull(services.Tasks.All.Single(task => task.Title == "Buy milk").Resolved);
        Assert.Equal("1 resolved", screen.ResolvedLabel);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Unticking_a_resolved_tasks_checkbox_returns_it_to_the_open_list_and_hides_the_count_at_zero()
    {
        var services = new ListServices();
        services.Add("Open one");
        services.AddResolved("Done one");
        using var screen = new TaskListScreen(services);
        screen.Click("1 resolved");

        screen.ClickCheckBox("Done one");

        Assert.Null(services.Tasks.All.Single(task => task.Title == "Done one").Resolved);
        Assert.Contains("Done one", screen.RowTitles);
        Assert.Null(screen.ResolvedLabel);
    }

}

[UX]
[Collection(ScreenTests.Collection)]
public class TaskListDeleteTests
{
    private static readonly TimeSpan HalfTheBar = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PastTheBar = TimeSpan.FromSeconds(15);

    [WpfFact]
    public void Delete_in_the_right_click_menu_deletes_the_task_the_same_way()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.ChooseContextItem("Buy milk", "Delete");

        Assert.Empty(screen.RowTitles);
        Assert.True(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Undo_brings_the_task_back_in_its_place_and_it_is_never_deleted()
    {
        var services = new ListServices();
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);
        screen.PressOn("A", Key.Delete);

        screen.Click("Undo");

        Assert.Equal(["A", "B"], screen.RowTitles);
        Assert.False(screen.HasButton("Undo"));
        Assert.Equal(2, services.Tasks.All.Count);
    }

    [WpfFact]
    public void Closing_the_window_deletes_the_task_waiting_on_the_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Delete);

        screen.Window.Close();

        Assert.Empty(services.Tasks.All);
    }

}
