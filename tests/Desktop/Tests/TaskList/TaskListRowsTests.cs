using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
[Collection(ScreenTests.Collection)]
public class TaskListRowsTests
{
    [WpfFact]
    public void Shows_the_open_tasks_in_rank_order_and_hides_the_resolved_ones()
    {
        var services = new ListServices();
        services.Add("Third");
        services.Add("Second");
        services.Add("First");
        services.AddResolved("Done");
        using var screen = new TaskListScreen(services);

        Assert.Equal(services.OpenTitlesInRankOrder(), screen.RowTitles);
        Assert.Equal(["First", "Second", "Third"], screen.RowTitles);
    }

    [WpfFact]
    public void Rows_show_no_tag_badges_and_each_group_heading_shows_its_tag()
    {
        var services = new ListServices();
        services.AddOlder("Buy milk", "", "home", "errand");
        using var screen = new TaskListScreen(services);

        Assert.False(screen.RowsShowBadges);
        Assert.Equal(["errand", "home"], screen.HeadingBadgeTags);
    }

    [WpfFact]
    public void Clicking_a_title_selects_the_task_and_shows_its_description()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Remember the oat milk");
        using var screen = new TaskListScreen(services);

        screen.ClickTitle("Buy milk");

        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.Contains("Remember the oat milk", screen.Web.Page);
    }

    [WpfFact]
    public async Task Clicking_a_link_in_a_title_opens_it_and_does_not_select_the_task()
    {
        var services = new ListServices();
        services.Add("Read [the docs](https://example.com/docs)", "Notes");
        using var screen = new TaskListScreen(services);

        screen.ClickTitleLink("the docs");
        await services.Browser.WaitForOpensAsync(1);

        Assert.Equal(["https://example.com/docs"], services.Browser.Opened);
        Assert.Empty(screen.SelectedTitles);
    }

    [WpfFact]
    public void Clicking_a_task_link_in_a_title_selects_that_task_not_the_clicked_row_and_opens_no_browser()
    {
        var services = new ListServices();
        var target = services.Add("Call Ana", "About the invoice");
        services.Add($"Read [the call]({services.Tasks.LinkAddress(target.Id)})", "Notes");
        using var screen = new TaskListScreen(services);

        screen.ClickTitleLink("the call");

        Assert.Equal(["Call Ana"], screen.SelectedTitles);
        Assert.Empty(services.Browser.Opened);
    }

    [WpfFact]
    public void Selecting_another_task_shows_the_other_description()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Remember the oat milk");
        services.Add("Call Ana", "About the invoice");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.ClickRow("Call Ana");

        Assert.Equal(["Call Ana"], screen.SelectedTitles);
        Assert.Contains("About the invoice", screen.Web.Page);
    }

    [WpfFact]
    public void A_task_link_in_a_description_selects_that_task()
    {
        var services = new ListServices();
        var target = services.Add("Call Ana", "About the invoice");
        services.Add("Buy milk", "See the other task");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.Web.ClickLink(services.Tasks.LinkAddress(target.Id));

        Assert.Equal(["Call Ana"], screen.SelectedTitles);
    }

    [WpfFact]
    public void A_browser_that_cannot_open_a_link_shows_the_error_in_the_message_line()
    {
        var services = new ListServices();
        services.Browser.Failure = new InvalidOperationException("No browser is installed.");
        services.Add("Buy milk", "See the shop");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.Web.ClickLink("https://example.com/shop");

        Assert.True(screen.ShowsText("No browser is installed."));
    }

}
