using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 3, resolving and deleting: the checkbox resolves a task and counts it, the resolved list can be shown and a task
/// unticked, Delete shows the Undo bar and Undo brings the task back, and closing the window makes a waiting delete final.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewResolveAndDeletePathTests
{
    [WpfFact]
    public void Resolving_unresolving_deleting_and_undoing_end_with_a_delete_that_closing_makes_final()
    {
        var services = new ListServices();
        services.Add("Call Ana");
        services.Add("Buy milk");
        services.AddResolved("Done one");
        using var screen = new TaskListScreen(services);

        screen.ClickCheckBox("Buy milk");
        Assert.Equal(["Call Ana"], screen.RowTitles);
        Assert.NotNull(services.Stored("Buy milk").Resolved);
        Assert.Equal("2 resolved", screen.ResolvedLabel);
        Assert.False(screen.HasButton("Undo"));

        screen.Click("2 resolved");
        screen.ClickCheckBox("Buy milk");
        Assert.Null(services.Stored("Buy milk").Resolved);
        Assert.Contains("Buy milk", screen.RowTitles);

        screen.PressOn("Call Ana", Key.Delete);
        Assert.DoesNotContain("Call Ana", screen.RowTitles);
        Assert.True(screen.HasButton("Undo"));
        screen.Click("Undo");
        Assert.Contains("Call Ana", screen.RowTitles);
        Assert.False(screen.HasButton("Undo"));
        Assert.Equal(3, services.Tasks.All.Count);

        screen.ChooseContextItem("Call Ana", "Delete");
        Assert.True(screen.HasButton("Undo"));
        screen.Window.Close();

        Assert.DoesNotContain(services.Tasks.All, task => task.Title == "Call Ana");
    }
}
