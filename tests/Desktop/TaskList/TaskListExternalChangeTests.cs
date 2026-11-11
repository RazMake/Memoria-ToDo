using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Changes made from outside the window, as Sync will make them, are made through the same Tasks calls (SaveEdit, Resolve,
/// Reopen, Add, Delete), which raise Tasks' Changed event. The list shows them at once. An open edit keeps the user's
/// typed text and saves as Tasks' edit merge decides.
/// </summary>
[UX]
public class TaskListExternalChangeTests
{
    private static TaskItem Only(ListServices services, string title) => services.Tasks.All.Single(task => task.Title == title);

    private static TaskItem ChangeElsewhere(ListServices services, string title, string newTitle, string? newDescription = null)
    {
        var started = Only(services, title);
        return services.Tasks.SaveEdit(started, new TaskDraft(started.Id, newTitle, newDescription ?? started.Description, started.Tags));
    }

    [WpfFact]
    public void A_task_edited_elsewhere_shows_its_new_title()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);

        ChangeElsewhere(services, "Buy milk", "Buy oat milk");

        Assert.Equal(["Call Ana", "Buy oat milk"], screen.RowTitles);
    }

    [WpfFact]
    public void An_open_task_edited_elsewhere_shows_its_new_description()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Two litres");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        ChangeElsewhere(services, "Buy milk", "Buy milk", "Three litres");

        Assert.Equal(["Buy milk"], screen.ExpandedTitles);
        Assert.Contains("Three litres", screen.Web.Page);
    }

    [WpfFact]
    public void A_task_added_elsewhere_shows_at_the_top()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        services.Add("Call Ana");

        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void A_task_resolved_elsewhere_leaves_the_list_and_counts_as_resolved()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);

        services.Tasks.Resolve(Only(services, "Buy milk").Id);

        Assert.Equal(["Call Ana"], screen.RowTitles);
        Assert.Equal("1 resolved", screen.ResolvedLabel);
    }

    [WpfFact]
    public void A_task_reopened_elsewhere_comes_back_to_the_list()
    {
        var services = new ListServices();
        var task = services.AddResolved("Buy milk");
        using var screen = new TaskListScreen(services);
        Assert.Empty(screen.RowTitles);

        services.Tasks.Reopen(task.Id);

        Assert.Equal(["Buy milk"], screen.RowTitles);
        Assert.Null(screen.ResolvedLabel);
    }

    [WpfFact]
    public void A_task_deleted_elsewhere_leaves_the_list()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);

        services.Tasks.Delete(Only(services, "Buy milk").Id);

        Assert.Equal(["Call Ana"], screen.RowTitles);
    }

    [WpfFact]
    public void A_change_elsewhere_keeps_the_filter_and_the_search()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickChip("work");
        screen.Search("call");

        services.Add("Call Bob", "", "work");
        services.Add("Call Eve", "", "home");

        Assert.Equal(["Call Bob", "Call Ana"], screen.RowTitles);
        Assert.Equal("call", screen.SearchText);
    }

    [WpfFact]
    public void A_change_elsewhere_keeps_what_the_user_typed_in_an_open_edit()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Two litres");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        ChangeElsewhere(services, "Buy milk", "Buy milk", "Three litres");

        Assert.True(screen.IsEditingAny);
        Assert.Equal("Buy oat milk", screen.EditedTitleText);
    }

    [WpfFact]
    public void Saving_an_edit_after_a_change_elsewhere_merges_the_two_as_Tasks_decides()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Two litres");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        ChangeElsewhere(services, "Buy milk", "Buy milk", "Three litres");

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Tasks.All.Single();
        Assert.Equal("Buy oat milk", saved.Title);
        Assert.Equal("Three litres", saved.Description);
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void Saving_an_edit_of_a_task_resolved_elsewhere_keeps_it_resolved_with_the_new_title()
    {
        var services = new ListServices();
        var task = services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        services.Tasks.Resolve(task.Id);

        Assert.True(screen.IsEditingAny);
        Assert.Equal("Buy oat milk", screen.EditedTitleText);
        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Tasks.All.Single();
        Assert.Equal("Buy oat milk", saved.Title);
        Assert.NotNull(saved.Resolved);
    }

    [WpfFact]
    public void Saving_an_edit_of_a_task_deleted_elsewhere_keeps_the_editor_open_and_says_so()
    {
        var services = new ListServices();
        var task = services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        services.Tasks.Delete(task.Id);

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.True(screen.IsEditingAny);
        Assert.Equal("Buy oat milk", screen.EditedTitleText);
        Assert.True(screen.ShowsText("This task no longer exists. Copy your changes before leaving the editor."));
    }
}
