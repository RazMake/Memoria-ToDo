using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
public class TaskListRowsTests
{
    [WpfFact]
    public void Shows_the_open_tasks_in_rank_order()
    {
        var services = new ListServices();
        services.Add("Third");
        services.Add("Second");
        services.Add("First");
        using var screen = new TaskListScreen(services);

        Assert.Equal(services.OpenTitlesInRankOrder(), screen.RowTitles);
        Assert.Equal(["First", "Second", "Third"], screen.RowTitles);
    }

    [WpfFact]
    public void Hides_resolved_tasks_from_the_list()
    {
        var services = new ListServices();
        services.Add("Open one");
        services.AddResolved("Done one");
        using var screen = new TaskListScreen(services);

        Assert.Equal(["Open one"], screen.RowTitles);
    }

    [WpfFact]
    public void Shows_a_title_as_a_reader_sees_it_without_Markdown_characters()
    {
        var services = new ListServices();
        services.Add("Buy **milk**");
        using var screen = new TaskListScreen(services);

        Assert.True(screen.ShowsText("Buy milk"));
        Assert.DoesNotContain(screen.Texts, text => text.Contains("**"));
    }

    [WpfFact]
    public void Names_a_row_by_its_plain_title_including_a_link_at_the_end()
    {
        var services = new ListServices();
        services.Add("Read [the docs](https://example.com/docs)");
        using var screen = new TaskListScreen(services);

        Assert.Equal(["Read the docs"], screen.RowTitles);
    }

    [WpfFact]
    public void Shows_each_rows_tags()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home", "errand");
        using var screen = new TaskListScreen(services);

        Assert.True(screen.ShowsText("home", screen.Row("Buy milk")));
        Assert.True(screen.ShowsText("errand", screen.Row("Buy milk")));
    }

    [WpfFact]
    public void Shows_each_row_with_a_checkbox_that_is_not_checked()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        Assert.False(screen.IsChecked("Buy milk"));
    }

    [WpfFact]
    public void Enter_expands_the_selected_task_and_shows_its_description_in_the_web_view()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Remember the oat milk");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.Enter);

        Assert.Equal(["Buy milk"], screen.ExpandedTitles);
        Assert.Contains("Remember the oat milk", screen.Web.Page);
    }

    [WpfFact]
    public void Clicking_a_row_expands_it()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Remember the oat milk");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk");

        Assert.Equal(["Buy milk"], screen.ExpandedTitles);
    }

    [WpfFact]
    public async Task Clicking_a_link_in_a_title_opens_it_and_does_not_expand_the_row()
    {
        var services = new ListServices();
        services.Add("Read [the docs](https://example.com/docs)", "Notes");
        using var screen = new TaskListScreen(services);

        screen.ClickTitleLink("the docs");
        await services.Browser.WaitForOpensAsync(1);

        Assert.Equal(["https://example.com/docs"], services.Browser.Opened);
        Assert.Empty(screen.ExpandedTitles);
    }

    [WpfFact]
    public async Task Clicking_a_link_in_a_title_opens_it_and_does_not_make_its_row_the_keyboard_target()
    {
        var services = new ListServices();
        services.Add("Read [the docs](https://example.com/docs)", "Notes");
        services.Add("Buy milk", "Remember the oat milk");
        using var screen = new TaskListScreen(services);
        screen.Select("Buy milk");

        screen.ClickTitleLink("the docs");
        await services.Browser.WaitForOpensAsync(1);

        Assert.Equal(["https://example.com/docs"], services.Browser.Opened);
        Assert.False(screen.RowWithLinkIsKeyboardTarget("the docs"));
    }

    [WpfFact]
    public void Clicking_a_task_link_in_a_title_expands_that_task_not_the_clicked_row_and_opens_no_browser()
    {
        var services = new ListServices();
        var target = services.Add("Call Ana", "About the invoice");
        services.Add($"Read [the call]({services.Tasks.LinkAddress(target.Id)})", "Notes");
        using var screen = new TaskListScreen(services);

        screen.ClickTitleLink("the call");

        Assert.Equal(["Call Ana"], screen.ExpandedTitles);
        Assert.Empty(services.Browser.Opened);
    }

    [WpfFact]
    public void A_browser_that_cannot_open_a_link_in_a_title_shows_the_error_in_the_message_line()
    {
        var services = new ListServices();
        services.Browser.Failure = new InvalidOperationException("No browser is installed.");
        services.Add("Read [the docs](https://example.com/docs)", "Notes");
        using var screen = new TaskListScreen(services);

        screen.ClickTitleLink("the docs");

        Assert.True(screen.ShowsText("No browser is installed."));
    }

    [WpfFact]
    public void Enter_on_the_open_task_collapses_it()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Remember the oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.PressOn("Buy milk", Key.Enter);

        Assert.Empty(screen.ExpandedTitles);
    }

    [WpfFact]
    public void Expanding_another_task_collapses_the_open_one_and_shows_the_other_description()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Remember the oat milk");
        services.Add("Call Ana", "About the invoice");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.PressOn("Call Ana", Key.Enter);

        Assert.Equal(["Call Ana"], screen.ExpandedTitles);
        Assert.Contains("About the invoice", screen.Web.Page);
    }

    [WpfFact]
    public void Offers_Edit_and_Delete_on_the_open_task()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.Enter);

        Assert.True(screen.HasButton("Edit"));
        Assert.True(screen.HasButton("Delete"));
    }

    [WpfFact]
    public void Shows_the_open_description_in_the_dark_page_after_the_theme_turns_dark()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Remember the oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);
        var before = screen.Web.Pages.Count;

        services.Theme.Switch(true);

        Assert.True(screen.Web.Pages.Count > before);
        Assert.Contains("Remember the oat milk", screen.Web.Page);
    }

    [WpfFact]
    public void A_task_link_in_a_description_expands_that_task()
    {
        var services = new ListServices();
        var target = services.Add("Call Ana", "About the invoice");
        services.Add("Buy milk", "See the other task");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Web.ClickLink(services.Tasks.LinkAddress(target.Id));

        Assert.Equal(["Call Ana"], screen.ExpandedTitles);
    }

    [WpfFact]
    public void A_link_to_a_deleted_task_says_it_no_longer_exists_and_keeps_the_open_task()
    {
        var services = new ListServices();
        var gone = services.Add("Old plan");
        var address = services.Tasks.LinkAddress(gone.Id);
        services.Tasks.Delete(gone.Id);
        services.Add("Buy milk", "See the old plan");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Web.ClickLink(address);

        Assert.True(screen.ShowsText("no longer exists"));
        Assert.Equal(["Buy milk"], screen.ExpandedTitles);
    }

    [WpfFact]
    public void A_link_to_a_resolved_task_shows_the_resolved_tasks_and_expands_it()
    {
        var services = new ListServices();
        var done = services.AddResolved("Old plan", "How it went");
        services.Add("Buy milk", "See the old plan");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Web.ClickLink(services.Tasks.LinkAddress(done.Id));

        Assert.True(screen.HasRow("Old plan"));
        Assert.Equal(["Old plan"], screen.ExpandedTitles);
    }

    [WpfFact]
    public async Task A_web_link_opens_in_the_default_browser()
    {
        var services = new ListServices();
        services.Add("Buy milk", "See the shop");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Web.ClickLink("https://example.com/shop");
        await services.Browser.WaitForOpensAsync(1);

        Assert.Equal(["https://example.com/shop"], services.Browser.Opened);
    }

    [WpfFact]
    public void Any_other_link_does_nothing()
    {
        var services = new ListServices();
        services.Add("Buy milk", "See the file");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Web.ClickLink("file:///C:/secrets.txt");

        Assert.Empty(services.Browser.Opened);
        Assert.Equal(["Buy milk"], screen.ExpandedTitles);
    }

    [WpfFact]
    public void A_browser_that_cannot_open_a_link_shows_the_error_in_the_message_line()
    {
        var services = new ListServices();
        services.Browser.Failure = new InvalidOperationException("No browser is installed.");
        services.Add("Buy milk", "See the shop");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Web.ClickLink("https://example.com/shop");

        Assert.True(screen.ShowsText("No browser is installed."));
    }

    [WpfFact]
    public void A_web_view_that_cannot_start_shows_the_description_as_source_and_the_list_keeps_working()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Some **bold** words");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.Enter);

        screen.Web.Fail("The WebView2 Runtime is missing.");

        Assert.Contains("Some **bold** words", screen.ReadOnlyFieldTexts);
        Assert.True(screen.ShowsText("The WebView2 Runtime is missing."));
        screen.PressOn("Call Ana", Key.Enter);
        Assert.Equal(["Call Ana"], screen.ExpandedTitles);
    }
}
