using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 4, reordering: Alt+Down moves a task one place down, a task dropped on the lower half of a row goes after it, and a group
/// heading dropped on another group moves the group. The drops are made as the window's drop handlers receive them.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewReorderPathTests
{
    [WpfFact]
    public void Alt_Down_and_drops_move_a_task_within_its_group_and_a_group_among_the_groups()
    {
        var services = new ListServices();
        services.Add("Home one", "", "home");
        services.Add("C", "", "work");
        services.Add("B", "", "work");
        services.Add("A", "", "work");
        services.TagOrder.Seed("home", "work");
        using var screen = new TaskListScreen(services);

        screen.PressOn("A", Key.LeftAlt, Key.Down);
        Assert.Equal(["B", "A", "C"], screen.RowTitlesIn("work"));
        Assert.Equal(["B", "A", "C", "Home one"], services.OpenTitlesInRankOrder());

        var taken = screen.Drop(services.Stored("A"), "C", lowerHalf: true, onGroup: "work");
        Assert.True(taken);
        Assert.Equal(["B", "C", "A"], screen.RowTitlesIn("work"));
        Assert.Equal(["B", "C", "A", "Home one"], services.OpenTitlesInRankOrder());

        screen.DropGroup("work", "home");
        Assert.Equal(["work", "home"], screen.GroupNames);
        Assert.Equal([("work", (string?)null, (string?)"home")], services.TagOrder.Moves);
        Assert.False(screen.HasButton("Undo"));
    }
}
