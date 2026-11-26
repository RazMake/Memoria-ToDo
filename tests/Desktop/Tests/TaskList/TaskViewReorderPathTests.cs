using System.IO;
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
        // A millisecond between adds orders the IDs as added, so a moved task that took another group's rank would show after it.
        services.Add("Home one", "", "home");
        services.Time.Advance(TimeSpan.FromMilliseconds(1));
        services.Add("C", "", "work");
        services.Time.Advance(TimeSpan.FromMilliseconds(1));
        services.Add("B", "", "work");
        services.Time.Advance(TimeSpan.FromMilliseconds(1));
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

        // The move is saved, then its notices fail before the window hears of the change: only a reload shows the saved order.
        services.TagOrder.SavesBeforeFailing = true;
        services.TagOrder.Failure = new AggregateException(new IOException("tags.json notice failed."), new IOException("Second notice failed."));
        screen.DropGroup("home", "work");
        services.TagOrder.Failure = null;
        services.TagOrder.SavesBeforeFailing = false;
        Assert.True(screen.ShowsText("The change may be only partly saved: tags.json notice failed."));
        Assert.Equal(["home", "work"], screen.GroupNames);

        services.NoticesFail = true;
        screen.PressOn("B", Key.LeftAlt, Key.Down);
        services.NoticesFail = false;
        Assert.True(screen.ShowsText("The change may be only partly saved: Changed failed."));
        Assert.Equal(["C", "B", "A"], screen.RowTitlesIn("work"));
        Assert.Equal(["C", "B", "A", "Home one"], services.OpenTitlesInRankOrder());
    }
}
