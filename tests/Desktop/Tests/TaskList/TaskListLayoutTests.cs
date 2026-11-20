using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The two sides of Task View: the groups of rows on the left and the selected task on the right, how a row's checkbox and
/// title line up, and what a click, a right-click and a checkbox click do. Unit tests cover the grouping and ordering rules.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListLayoutTests
{
    private const string LongTitle =
        "Buy milk and eggs and bread and cheese and apples and pears and plums and a very long list of other things for the whole week";

    [WpfFact]
    public void The_window_has_the_rows_on_the_left_and_the_description_on_the_right_with_a_bar_between()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        Assert.True(screen.HasSplitter);
        var edges = screen.SideEdges;
        Assert.True(edges.RowsRight <= edges.SplitterLeft);
        Assert.True(edges.SplitterRight <= edges.DetailsLeft);
    }

    [WpfFact]
    public void The_rows_side_starts_with_68_percent_of_the_width_and_the_description_with_32()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.ResizeTo(1600);

        Assert.InRange(screen.RowsSideShare, 0.67, 0.69);
    }

    [WpfFact]
    public void Dragging_the_bar_to_the_right_makes_the_rows_side_wider()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.ResizeTo(1600);
        var before = screen.RowsSideWidth;

        screen.MoveSplitter(40);

        Assert.True(screen.RowsSideWidth > before);
    }

    [WpfFact]
    public void The_checkbox_lines_up_with_the_middle_of_the_titles_first_line()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        Assert.InRange(screen.CheckBoxGap("Buy milk").Gap, -1, 1);
    }

    [WpfTheory]
    [InlineData(12)]
    [InlineData(24)]
    public void The_checkbox_stays_lined_up_with_the_first_line_of_a_title_that_wraps_at_any_text_size(double points)
    {
        var services = new ListServices();
        services.Add(LongTitle);
        using var screen = new TaskListScreen(services);
        screen.ResizeTo(500);
        screen.SetTextSize(points);

        var (gap, lines) = screen.CheckBoxGap(LongTitle);

        Assert.True(lines > 1, "the title must wrap for this test to mean anything");
        Assert.InRange(gap, -1, 1);
    }

    [WpfFact]
    public void The_cursor_is_a_hand_over_a_checkbox_but_not_over_a_title()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        Assert.Equal(Cursors.Hand, screen.CheckBoxCursor("Buy milk"));
        Assert.NotEqual(Cursors.Hand, screen.TitleCursor("Buy milk"));
    }

    [WpfFact]
    public void Clicking_a_checkbox_resolves_the_task_and_hides_it_behind_the_resolved_count_with_no_Undo()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);

        screen.ClickCheckBox("Buy milk");

        Assert.Equal(["Call Ana"], screen.RowTitles);
        Assert.Equal("1 resolved", screen.ResolvedLabel);
        Assert.Equal(0, screen.CountButtons("Undo"));
        Assert.Empty(screen.SelectedTitles);
    }

    [WpfFact]
    public void Clicking_a_title_selects_the_task_and_does_not_resolve_it()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.ClickTitle("Buy milk");

        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.Equal(["Buy milk"], screen.RowTitles);
        Assert.Null(screen.ResolvedLabel);
    }

    [WpfFact]
    public void Right_clicking_a_row_selects_it_and_offers_Edit_Title_Edit_Description_and_Delete()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);

        screen.RightClick("Call Ana");

        Assert.Equal(["Call Ana"], screen.SelectedTitles);
        Assert.Equal([("Edit Title", "Shift+F2"), ("Edit Description", "F2"), ("Delete", "Del")], screen.ContextMenuItems("Call Ana"));
        Assert.False(screen.HasButton("Edit"));
    }

    [WpfFact]
    public void Nothing_selected_shows_the_prompt_and_an_empty_description_says_so()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        Assert.True(screen.ShowsText("Select a task to see its description.", screen.Details));

        screen.ClickRow("Buy milk");

        Assert.False(screen.ShowsText("Select a task to see its description.", screen.Details));
        Assert.True(screen.ShowsText("No description.", screen.Details));
    }

    [WpfFact]
    public void The_prompt_returns_when_the_selected_task_is_deleted()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.ChooseContextItem("Buy milk", "Delete");

        Assert.Empty(screen.SelectedTitles);
        Assert.True(screen.ShowsText("Select a task to see its description.", screen.Details));
    }

    [WpfFact]
    public void Groups_show_alphabetically_with_No_tag_last_and_a_task_with_two_tags_shows_in_both_and_is_selected_in_both()
    {
        var services = new ListServices();
        services.Add("Plain");
        services.AddOlder("Both", "", "work", "Home");
        using var screen = new TaskListScreen(services);

        Assert.Equal(["Home", "work", "No tag"], screen.GroupNames);
        Assert.Equal(["Both"], screen.RowTitlesIn("Home"));
        Assert.Equal(["Both"], screen.RowTitlesIn("work"));

        screen.ClickRow("Both", "Home");

        Assert.Equal(2, screen.SelectedRowCount);
    }
}
