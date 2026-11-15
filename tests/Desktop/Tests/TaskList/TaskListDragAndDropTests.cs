using System.Windows;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Drag and drop reorders a group. The tests raise the standard WPF drop events on a row, with an ordinary
/// <see cref="DataObject"/> built the way a row drag builds it (<see cref="DraggedTask"/>). A drop in a row's upper half puts the task before that row; in its lower half, after it.
/// The tests check the groups the window shows and the ranked tasks Tasks keeps, not how the window handles the events.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListDragAndDropTests
{
    private static TaskItem Task(ListServices services, string title) => services.Tasks.All.Single(task => task.Title == title);

    private static ListServices ThreeTasks()
    {
        var services = new ListServices();
        services.Add("C", "", "work");
        services.Add("B", "", "home");
        services.Add("A", "", "work");
        return services;
    }

    [WpfFact]
    public void Dropping_a_task_on_the_lower_half_of_a_row_of_its_group_puts_it_after_that_row()
    {
        var services = ThreeTasks();
        using var screen = new TaskListScreen(services);

        var taken = screen.Drop(Task(services, "A"), "C", lowerHalf: true, onGroup: "work");

        Assert.True(taken);
        Assert.Equal(["C", "A"], screen.RowTitlesIn("work"));
        Assert.Equal(["B"], screen.RowTitlesIn("home"));
        Assert.Equal(["B", "C", "A"], services.OpenTitlesInRankOrder());
    }

    [WpfFact]
    public void Dropping_a_task_on_a_row_of_another_group_moves_nothing_and_adds_no_tag()
    {
        var services = ThreeTasks();
        using var screen = new TaskListScreen(services);

        screen.Drop(Task(services, "A"), "B", lowerHalf: true, onGroup: "home");

        Assert.Equal(["A", "C"], screen.RowTitlesIn("work"));
        Assert.Equal(["B"], screen.RowTitlesIn("home"));
        Assert.Equal(["A", "B", "C"], services.OpenTitlesInRankOrder());
        Assert.Equal(["work"], Task(services, "A").Tags);
    }

}
