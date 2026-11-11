using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// A task moves only within its group, open tasks among open tasks and resolved tasks among resolved tasks. Moving never
/// resolves or reopens a task, so a drop on the other group, and Alt+Up or Alt+Down at a group's end, move nothing.
/// The drop is the one in <see cref="TaskListDragAndDropTests"/>: a <c>DataObject</c> holding the <c>TaskItem</c>.
/// </summary>
[UX]
public class TaskListGroupReorderTests
{
    private static TaskItem Task(ListServices services, string title) => services.Tasks.All.Single(task => task.Title == title);

    // Resolved tasks, in rank order: R1, R2, R3; open tasks: O1, O2.
    private static ListServices Both()
    {
        var services = new ListServices();
        services.AddResolved("R3");
        services.AddResolved("R2");
        services.AddResolved("R1");
        services.Add("O2");
        services.Add("O1");
        return services;
    }

    [WpfFact]
    public void Alt_Down_moves_a_resolved_task_one_place_among_the_resolved_tasks()
    {
        var services = Both();
        using var screen = new TaskListScreen(services);
        screen.Click("3 resolved");

        screen.PressOn("R1", Key.LeftAlt, Key.Down);

        Assert.Equal(["R2", "R1", "R3"], services.ResolvedTitlesInRankOrder());
        Assert.Equal(["O1", "O2", "R2", "R1", "R3"], screen.RowTitles);
        Assert.Equal("3 resolved", screen.ResolvedLabel);
    }

    [WpfFact]
    public void Dropping_a_resolved_task_on_a_resolved_row_moves_it_and_keeps_it_resolved()
    {
        var services = Both();
        using var screen = new TaskListScreen(services);
        screen.Click("3 resolved");

        screen.Drop(Task(services, "R3"), "R1");

        Assert.Equal(["R3", "R1", "R2"], services.ResolvedTitlesInRankOrder());
        Assert.NotNull(Task(services, "R3").Resolved);
    }

    [WpfFact]
    public void Dropping_an_open_task_on_a_resolved_row_moves_nothing()
    {
        var services = Both();
        var before = services.Tasks.All.ToList();
        using var screen = new TaskListScreen(services);
        screen.Click("3 resolved");

        screen.Drop(Task(services, "O1"), "R2");
        screen.Drop(Task(services, "O1"), "R3", lowerHalf: true);

        Assert.Equal(before, services.Tasks.All);
        Assert.Equal(["O1", "O2"], services.OpenTitlesInRankOrder());
        Assert.Equal(["R1", "R2", "R3"], services.ResolvedTitlesInRankOrder());
    }

    [WpfFact]
    public void Dropping_a_resolved_task_on_an_open_row_moves_nothing_and_does_not_reopen_it()
    {
        var services = Both();
        var before = services.Tasks.All.ToList();
        using var screen = new TaskListScreen(services);
        screen.Click("3 resolved");

        screen.Drop(Task(services, "R2"), "O1");
        screen.Drop(Task(services, "R2"), "O2", lowerHalf: true);

        Assert.Equal(before, services.Tasks.All);
        Assert.Equal(["O1", "O2"], services.OpenTitlesInRankOrder());
    }

    [WpfFact]
    public void Alt_Down_on_the_last_open_task_and_Alt_Up_on_the_first_resolved_task_do_not_cross_groups()
    {
        var services = Both();
        var before = services.Tasks.All.ToList();
        using var screen = new TaskListScreen(services);
        screen.Click("3 resolved");

        screen.PressOn("O2", Key.LeftAlt, Key.Down);
        screen.PressOn("R1", Key.LeftAlt, Key.Up);

        Assert.Equal(before, services.Tasks.All);
    }
}
