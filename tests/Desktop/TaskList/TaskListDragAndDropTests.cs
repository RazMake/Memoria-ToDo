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

    [WpfFact]
    public void Dropping_a_task_on_the_upper_half_of_a_row_puts_it_before_that_row()
    {
        var services = ThreeTasks();
        using var screen = new TaskListScreen(services);

        screen.Drop(Task(services, "C"), "A");

        Assert.Equal(["C", "A", "B"], screen.RowTitles);
        Assert.Equal(["C", "A", "B"], services.OpenTitlesInRankOrder());
    }

    [WpfFact]
    public void Dropping_a_task_where_it_already_shows_changes_nothing()
    {
        var services = ThreeTasks();
        var before = Task(services, "A");
        using var screen = new TaskListScreen(services);

        screen.Drop(before, "B");
        screen.Drop(before, "A");

        Assert.Equal(["A", "B", "C"], screen.RowTitles);
        Assert.Equal(before, Task(services, "A"));
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Dropping_a_task_among_the_tasks_shown_when_a_tag_filters_the_list_places_it_by_the_row_dropped_on()
    {
        var services = ThreeTasks();
        using var screen = new TaskListScreen(services);
        screen.ClickChip("work");

        screen.Drop(Task(services, "A"), "C", lowerHalf: true);

        Assert.Equal(["C", "A"], screen.RowTitles);
        Assert.Equal(["B", "C", "A"], services.OpenTitlesInRankOrder());
    }

    [WpfFact]
    public void Dropping_something_that_is_not_a_task_is_left_alone()
    {
        var services = ThreeTasks();
        using var screen = new TaskListScreen(services);

        var taken = screen.DropFiles("B", @"C:\Pictures\photo.png");

        Assert.False(taken);
        Assert.Equal(["A", "B", "C"], screen.RowTitles);
    }

    [WpfFact]
    public void Dropping_a_task_that_was_deleted_meanwhile_changes_nothing_and_says_so()
    {
        var services = ThreeTasks();
        var gone = Task(services, "A");
        using var screen = new TaskListScreen(services);
        services.Tasks.Delete(gone.Id);

        screen.Drop(gone, "C", lowerHalf: true);

        Assert.Equal(["B", "C"], screen.RowTitles);
        Assert.True(screen.ShowsText("no longer exists"));
    }

    [WpfFact]
    public void A_drop_whose_move_fails_leaves_the_order_and_says_why()
    {
        var services = ThreeTasks();
        using var screen = new TaskListScreen(services);
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.Drop(Task(services, "A"), "C", lowerHalf: true);

        Assert.Equal(["A", "B", "C"], screen.RowTitles);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
    }
}
