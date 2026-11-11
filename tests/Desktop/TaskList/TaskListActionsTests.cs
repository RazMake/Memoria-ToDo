using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
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

    [WpfFact]
    public void Alt_Up_moves_the_task_one_place_up()
    {
        var services = new ListServices();
        services.Add("C");
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);

        screen.PressOn("C", Key.LeftAlt, Key.Up);

        Assert.Equal(["A", "C", "B"], screen.RowTitles);
        Assert.Equal(["A", "C", "B"], services.OpenTitlesInRankOrder());
    }

    [WpfFact]
    public void Alt_Up_on_the_first_task_and_Alt_Down_on_the_last_do_nothing()
    {
        var services = new ListServices();
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);

        screen.PressOn("A", Key.LeftAlt, Key.Up);
        screen.PressOn("B", Key.LeftAlt, Key.Down);

        Assert.Equal(["A", "B"], screen.RowTitles);
    }

    [WpfFact]
    public void Alt_Down_moves_a_task_among_the_tasks_shown_when_a_tag_filters_the_list()
    {
        var services = new ListServices();
        services.Add("C", "", "work");
        services.Add("B");
        services.Add("A", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickChip("work");

        screen.PressOn("A", Key.LeftAlt, Key.Down);

        Assert.Equal(["C", "A"], screen.RowTitles);
        var order = services.OpenTitlesInRankOrder();
        Assert.True(order.ToList().IndexOf("C") < order.ToList().IndexOf("A"));
    }

    [WpfFact]
    public void A_move_that_cannot_be_saved_leaves_the_task_where_it_was_and_says_why()
    {
        var services = new ListServices();
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.PressOn("A", Key.LeftAlt, Key.Down);

        Assert.Equal(["A", "B"], screen.RowTitles);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
    }
}

[UX]
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
    public void Ctrl_D_resolves_the_selected_task()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.LeftCtrl, Key.D);

        Assert.NotNull(services.Tasks.All.Single().Resolved);
        Assert.Empty(screen.RowTitles);
    }

    [WpfFact]
    public void A_resolve_that_cannot_be_saved_leaves_the_task_open_and_says_why()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.ClickCheckBox("Buy milk");

        Assert.Equal(["Buy milk"], screen.RowTitles);
        Assert.Null(services.Tasks.All.Single().Resolved);
        Assert.True(screen.ShowsText("Writing is not allowed here"));
    }

    [WpfFact]
    public void Hides_the_resolved_count_while_nothing_is_resolved()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        Assert.Null(screen.ResolvedLabel);
    }

    [WpfFact]
    public void The_resolved_count_shows_the_resolved_tasks_in_rank_order_when_chosen()
    {
        var services = new ListServices();
        services.AddResolved("Older");
        services.AddResolved("Newer");
        using var screen = new TaskListScreen(services);
        Assert.Equal("2 resolved", screen.ResolvedLabel);
        Assert.False(screen.HasRow("Older"));

        screen.Click("2 resolved");

        Assert.Equal(["Newer", "Older"], screen.RowTitles);
    }

    [WpfFact]
    public void Reopen_returns_a_resolved_task_to_the_open_list_and_hides_the_count_at_zero()
    {
        var services = new ListServices();
        services.Add("Open one");
        services.AddResolved("Done one");
        using var screen = new TaskListScreen(services);
        screen.Click("1 resolved");

        screen.Click("Reopen", "Done one");

        Assert.Null(services.Tasks.All.Single(task => task.Title == "Done one").Resolved);
        Assert.Contains("Done one", screen.RowTitles);
        Assert.Null(screen.ResolvedLabel);
    }

    [WpfFact]
    public void Ctrl_D_reopens_a_selected_resolved_task()
    {
        var services = new ListServices();
        services.AddResolved("Done one");
        using var screen = new TaskListScreen(services);
        screen.Click("1 resolved");

        screen.PressOn("Done one", Key.LeftCtrl, Key.D);

        Assert.Null(services.Tasks.All.Single().Resolved);
    }
}

[UX]
public class TaskListDeleteTests
{
    private static readonly TimeSpan HalfTheBar = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PastTheBar = TimeSpan.FromSeconds(15);

    [WpfFact]
    public void Delete_hides_the_task_at_once_with_no_confirmation_and_shows_an_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.Delete);

        Assert.Equal(["Call Ana"], screen.RowTitles);
        Assert.True(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void The_Delete_button_deletes_the_open_task_the_same_way()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Click("Delete");

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
    public async Task Keeps_the_Undo_bar_for_a_while_then_removes_it_and_deletes_the_task()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Delete);

        await screen.PassTimeAsync(HalfTheBar);
        Assert.True(screen.HasButton("Undo"));
        Assert.Single(services.Tasks.All);

        await screen.PassTimeAsync(PastTheBar);

        Assert.False(screen.HasButton("Undo"));
        Assert.Empty(services.Tasks.All);
    }

    [WpfFact]
    public void Deleting_another_task_deletes_the_first_one_at_once_and_keeps_one_bar()
    {
        var services = new ListServices();
        services.Add("B");
        services.Add("A");
        using var screen = new TaskListScreen(services);
        screen.PressOn("A", Key.Delete);

        screen.PressOn("B", Key.Delete);

        Assert.DoesNotContain(services.Tasks.All, task => task.Title == "A");
        Assert.Single(services.Tasks.All);
        Assert.Equal(1, screen.CountButtons("Undo"));
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

    [WpfFact]
    public async Task A_task_file_that_cannot_be_deleted_comes_back_with_the_reason()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Delete);
        services.Files.FailDeletesUnder(services.TaskFolder);

        await screen.PassTimeAsync(PastTheBar);

        Assert.Equal(["Buy milk"], screen.RowTitles);
        Assert.Single(services.Tasks.All);
        Assert.True(screen.ShowsText("Deleting is not allowed here"));
    }

    [WpfFact]
    public async Task Images_that_cannot_be_removed_leave_the_task_deleted_and_say_so()
    {
        var services = new ListServices();
        var task = services.Add("Buy milk");
        var imagesFolder = Path.Combine(services.Clone, "images");
        services.Files.WriteFile(Path.Combine(imagesFolder, task.Id, "photo.png"), [1, 2, 3]);
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Delete);
        services.Files.FailDeletesUnder(imagesFolder);

        await screen.PassTimeAsync(PastTheBar);

        Assert.Empty(screen.RowTitles);
        Assert.Empty(services.Tasks.All);
        Assert.True(screen.ShowsText("The task was deleted, but some of its images couldn't be removed: "));
        Assert.True(screen.ShowsText("Deleting is not allowed here"));
    }

    [WpfFact]
    public void A_delete_that_fails_as_the_window_closes_shows_its_reason_the_next_time_it_opens()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Delete);
        services.Files.FailDeletesUnder(services.TaskFolder);
        screen.Window.Close();

        screen.View.Show();

        Assert.Equal(["Buy milk"], screen.RowTitles);
        Assert.True(screen.ShowsText("Deleting is not allowed here"));
    }
}
