using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.TaskEditor.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Mentions in Task View, on the common paths: a row and the selected description show the contact's current text, they show
/// again when contacts change, even from another thread, an open edit keeps its text, and its Preview shows live text too.
/// How a mention's text is chosen is in <see cref="LiveMentionsTests"/>.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListMentionTests
{
    private const string AnaOld = "[Old Name](contact:ana \"{fullName}\")";
    private const string ZedOld = "[Zed Old](contact:zed \"{fullName}\")";

    private static ListServices Services()
    {
        var services = new ListServices();
        services.UseContacts(["{fullName}"], ContactFiles.Person("ana", "Ana Pop"));
        return services;
    }

    private static Task Settled() => Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task;

    [WpfFact]
    public void A_row_shows_the_contacts_current_text_for_a_mention()
    {
        var services = Services();
        services.Add($"Call {AnaOld} today");
        using var screen = new TaskListScreen(services);

        Assert.Equal(["Call Ana Pop today"], screen.RowTitles);
    }

    [WpfFact]
    public void A_row_shows_the_stored_text_of_a_mention_Contacts_has_no_text_for()
    {
        var services = Services();
        services.Add($"Call {ZedOld} today");
        using var screen = new TaskListScreen(services);

        Assert.Equal(["Call Zed Old today"], screen.RowTitles);
    }

    [WpfFact]
    public void The_selected_description_shows_the_contacts_current_text_for_a_mention()
    {
        var services = Services();
        services.Add("Buy milk", $"Ask {AnaOld} first");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk");

        Assert.Contains("Ana Pop", screen.Web.Page);
        Assert.DoesNotContain("Old Name", screen.Web.Page);
    }

    [WpfFact]
    public void A_change_to_contacts_shows_the_rows_and_the_selected_description_again()
    {
        var services = Services();
        services.Add($"Call {AnaOld} today", $"Ask {AnaOld} first");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Call Ana Pop today");

        services.ChangeContacts(ContactFiles.Person("ana", "Ana Maria Pop"));

        Assert.Equal(["Call Ana Maria Pop today"], screen.RowTitles);
        Assert.Contains("Ana Maria Pop", screen.Web.Page);
    }

    [WpfFact]
    public async Task A_change_to_contacts_from_another_thread_shows_the_rows_and_the_selected_description_again()
    {
        var services = Services();
        services.Add($"Call {AnaOld} today", $"Ask {AnaOld} first");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Call Ana Pop today");

        await Task.Run(() => services.ChangeContacts(ContactFiles.Person("ana", "Ana Maria Pop")), TestContext.Current.CancellationToken);
        await Settled();

        Assert.Equal(["Call Ana Maria Pop today"], screen.RowTitles);
        Assert.Contains("Ana Maria Pop", screen.Web.Page);
    }

    [WpfFact]
    public void An_open_description_edit_keeps_its_text_when_contacts_change()
    {
        var services = Services();
        services.Add("Buy milk", $"Ask {AnaOld} first");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);

        services.ChangeContacts(ContactFiles.Person("ana", "Ana Maria Pop"));

        Assert.True(screen.IsEditingAny);
        Assert.Equal($"Ask {AnaOld} first", screen.EditedDescriptionText);
    }

    [WpfFact]
    public void An_open_title_edit_keeps_its_text_when_contacts_change()
    {
        var services = Services();
        services.Add($"Call {AnaOld} today");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Call Ana Pop today", Key.LeftShift, Key.F2);

        services.ChangeContacts(ContactFiles.Person("ana", "Ana Maria Pop"));

        Assert.True(screen.IsEditingAny);
        Assert.Equal($"Call {AnaOld} today", screen.EditedTitleText);
    }

    [WpfFact]
    public async Task An_edit_of_the_description_shows_a_mention_as_a_chip()
    {
        var services = Services();
        services.Add("Buy milk", $"Ask {AnaOld} first");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.F2);
        screen.Window.UpdateLayout();
        await RenderedField.Settled();

        Assert.True(RenderedField.Of(screen.EditedDescription).ShowsBackdrop(TextLooks.Light.MentionBackground, 1.0));
    }

    [WpfFact]
    public async Task An_edit_of_the_title_shows_a_mention_as_a_chip()
    {
        var services = Services();
        services.Add($"Call {AnaOld} today");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Call Ana Pop today", Key.LeftShift, Key.F2);
        screen.Window.UpdateLayout();
        await RenderedField.Settled();

        Assert.True(RenderedField.Of(screen.EditorTitle("Call Ana Pop today")).ShowsBackdrop(TextLooks.Light.MentionBackground, 1.0));
    }

    [WpfFact]
    public void Preview_shows_the_drafts_mentions_with_the_contacts_current_text()
    {
        var services = Services();
        services.Add("Buy milk", "Ask first");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription($"Ask {AnaOld} now");

        screen.ClickTab("Preview");

        Assert.Contains("Ana Pop", screen.Web.Page);
        Assert.DoesNotContain("Old Name", screen.Web.Page);
    }

    [WpfFact]
    public void Preview_shows_the_draft_again_when_contacts_change()
    {
        var services = Services();
        services.Add("Buy milk", "Ask first");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription($"Ask {AnaOld} now");
        screen.ClickTab("Preview");

        services.ChangeContacts(ContactFiles.Person("ana", "Ana Maria Pop"));

        Assert.Equal("Preview", screen.SelectedTab);
        Assert.Contains("Ana Maria Pop", screen.Web.Page);
    }
}
