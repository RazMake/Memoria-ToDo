using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What an edit's end and start do with the tag control: text typed in Custom and not chosen goes with the edit it was typed in,
/// and starting an edit finishes only the task's own "Tag changed" Undo bar. Esc and selecting another task are in the tag tests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListTagEditEndTests
{
    private static void TypeInCustom(TaskListScreen screen, string text)
    {
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags(text);
    }

    // ---- Text in Custom that was never chosen ----

    [WpfFact]
    public void Text_left_in_Custom_by_a_description_edit_is_not_chosen_by_the_save_of_a_title_edit_that_follows()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        TypeInCustom(screen, "pend");

        screen.ChooseContextItem("Buy milk", "Edit Title");
        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.Press(Key.Enter);

        var saved = services.Stored("Buy oat milk");
        Assert.Equal(["home"], saved.Tags);
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void Text_left_in_Custom_by_a_title_edit_is_not_chosen_by_the_save_of_a_description_edit_that_follows()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);
        TypeInCustom(screen, "pend");

        screen.ChooseContextItem("Buy milk", "Edit Description");
        screen.TypeDescription("Oat milk, two cartons");
        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Stored("Buy milk");
        Assert.Equal(["home"], saved.Tags);
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void Text_left_in_Custom_is_not_a_change_of_the_edit_so_leaving_it_asks_for_nothing_and_shows_no_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        TypeInCustom(screen, "pend");

        screen.ChooseContextItem("Buy milk", "Edit Title");

        Assert.True(screen.IsEditingTitle("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
    }

    // ---- The task's tag-change bar ----

    [WpfFact]
    public void Starting_a_title_edit_finishes_the_tasks_Tag_changed_bar_and_keeps_the_change()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        Assert.True(screen.HasButton("Undo"));

        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);

        Assert.True(screen.IsEditingTitle("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
        Assert.Equal(["work"], services.Stored("Buy milk").Tags);
        Assert.Equal("work", screen.PillTag("Buy milk"));
    }

    [WpfFact]
    public void Starting_a_title_edit_of_another_task_leaves_the_Tag_changed_bar_and_its_Undo_still_works()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");

        screen.PressOn("Call Ana", Key.LeftShift, Key.F2);

        Assert.True(screen.IsEditingTitle("Call Ana"));
        Assert.True(screen.HasButton("Undo"));
        screen.Click("Undo");
        Assert.Equal(["home"], services.Stored("Buy milk").Tags);
    }
}
