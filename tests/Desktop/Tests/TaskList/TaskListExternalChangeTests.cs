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
[Collection(ScreenTests.Collection)]
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

}
