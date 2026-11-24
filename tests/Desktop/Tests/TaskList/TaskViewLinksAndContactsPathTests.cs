using Memoria.ToDo.Desktop.TaskEditor.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 9, links and live contacts: a task link in a title or a description selects that task, a web link opens the browser,
/// a browser that can't open it shows the error, and a change to Contacts shows a mention's new text in the row and the description.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewLinksAndContactsPathTests
{
    [WpfFact]
    public async Task Links_select_tasks_or_open_the_browser_and_a_contacts_change_updates_mentions()
    {
        var services = new ListServices();
        services.UseContacts(["{fullName}"], ContactFiles.Person("ana", "Ana Pop"));
        var target = services.Add("Call Ana", "About the invoice");
        services.Add($"Read [the call]({services.Tasks.LinkAddress(target.Id)})", "Notes");
        services.Add("Read [the docs](https://example.com/docs)", "Notes");
        services.Add("Buy milk", $"See the other task and the shop. Ask [Old Name](contact:ana \"{{fullName}}\") first.");
        services.Add("Call [Old Name](contact:ana \"{fullName}\") today");
        using var screen = new TaskListScreen(services);
        Assert.Contains("Call Ana Pop today", screen.RowTitles);

        screen.ClickTitleLink("the call");
        Assert.Equal(["Call Ana"], screen.SelectedTitles);
        Assert.Empty(services.Browser.Opened);

        screen.ClickTitleLink("the docs");
        await services.Browser.WaitForOpensAsync(1);
        Assert.Equal(["https://example.com/docs"], services.Browser.Opened);
        Assert.Equal(["Call Ana"], screen.SelectedTitles);

        screen.ClickRow("Buy milk");
        Assert.Contains("Ana Pop", screen.Web.Page);
        screen.Web.ClickLink(services.Tasks.LinkAddress(target.Id));
        Assert.Equal(["Call Ana"], screen.SelectedTitles);

        screen.ClickRow("Buy milk");
        services.Browser.Failure = new InvalidOperationException("No browser is installed.");
        screen.Web.ClickLink("https://example.com/shop");
        Assert.True(screen.ShowsText("No browser is installed."));

        services.ChangeContacts(ContactFiles.Person("ana", "Ana Maria Pop"));
        Assert.Contains("Call Ana Maria Pop today", screen.RowTitles);
        Assert.Contains("Ana Maria Pop", screen.Web.Page);
    }
}
