using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Collapsing a group: rows are indented, a click on the tag badge or "No tag" toggles, and the keys skip hidden rows.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListGroupCollapseTests
{
    private static ListServices HomeAndWork()
    {
        var services = new ListServices();
        services.Add("Work two", "", "work");
        services.Add("Work one", "About work one", "work");
        services.Add("Home one", "", "home");
        services.Add("Plain");
        return services;
    }

    [WpfFact]
    public void Rows_are_indented_under_their_group_heading()
    {
        using var screen = new TaskListScreen(HomeAndWork());

        Assert.True(screen.RowIndent("Home one", "home") > 0);
    }

    [WpfFact]
    public void A_click_on_the_tag_badge_hides_the_groups_rows_and_another_click_shows_them()
    {
        using var screen = new TaskListScreen(HomeAndWork());

        screen.ToggleGroup("work");
        Assert.Equal(["Home one", "Plain"], screen.RowTitles);
        screen.ToggleGroup("work");

        Assert.Equal(["Home one", "Work one", "Work two", "Plain"], screen.RowTitles);
    }

    [WpfFact]
    public void A_click_on_the_No_tag_heading_collapses_that_group_too()
    {
        using var screen = new TaskListScreen(HomeAndWork());

        screen.ToggleGroup("No tag");

        Assert.DoesNotContain("Plain", screen.RowTitles);
    }

    [WpfFact]
    public void The_tag_badge_shows_the_hand_pointer()
    {
        using var screen = new TaskListScreen(HomeAndWork());

        Assert.Equal(Cursors.Hand, screen.GroupCursor("work"));
    }

    [WpfFact]
    public void A_collapsed_heading_shows_how_many_tasks_it_hides()
    {
        using var screen = new TaskListScreen(HomeAndWork());

        screen.ToggleGroup("work");

        Assert.Contains("(2)", screen.HeadingText("work"));
    }

    [WpfFact]
    public void Down_skips_the_rows_of_a_collapsed_group()
    {
        using var screen = new TaskListScreen(HomeAndWork());
        screen.ToggleGroup("work");
        screen.Select("Home one");

        screen.Press(Key.Down);

        Assert.True(screen.RowIsKeyboardTarget("Plain"));
    }

    [WpfFact]
    public void A_collapsed_group_keeps_the_selection_and_the_right_side_shows_the_task_until_the_group_expands()
    {
        using var screen = new TaskListScreen(HomeAndWork());
        screen.ClickRow("Work one");

        screen.ToggleGroup("work");

        Assert.DoesNotContain("Work one", screen.RowTitles);
        Assert.True(screen.DescriptionIsShown);
        Assert.Contains("About work one", screen.Web.Page);
        Assert.False(screen.ShowsText("Select a task to see its description.", screen.Details));

        screen.ToggleGroup("work");

        Assert.Equal(["Work one"], screen.SelectedTitles);
        Assert.True(screen.DescriptionIsShown);
    }

    [WpfFact]
    public void Following_a_link_to_a_task_in_a_collapsed_group_expands_it()
    {
        var services = new ListServices();
        var target = services.Add("Target", "", "work");
        services.Add("Source [see](" + services.Tasks.LinkAddress(target.Id) + ")", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ToggleGroup("work");

        screen.ClickTitleLink("see");

        Assert.Contains("Target", screen.RowTitlesIn("work"));
    }
}
