using System.Windows;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Drag and drop reorders the list. The tests raise the standard WPF drop events on a row, with an ordinary
/// <see cref="DataObject"/> built from the dragged <see cref="TaskItem"/> (<c>new DataObject(task)</c>, read back by
/// <c>GetData(typeof(TaskItem))</c>). A drop in a row's upper half puts the task before that row; in its lower half, after it.
/// The tests check the ranked tasks Tasks keeps, not how the window handles the events.
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
    public void Dropping_a_task_on_the_lower_half_of_a_row_puts_it_after_that_row()
    {
        var services = ThreeTasks();
        using var screen = new TaskListScreen(services);

        var taken = screen.Drop(Task(services, "A"), "C", lowerHalf: true);

        Assert.True(taken);
        Assert.Equal(["B", "C", "A"], screen.RowTitles);
        Assert.Equal(["B", "C", "A"], services.OpenTitlesInRankOrder());
    }

}
