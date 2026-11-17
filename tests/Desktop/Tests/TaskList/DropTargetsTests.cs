using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The one rule that picks the gap a dragged task or group lands in, from where the list's items sit and where the pointer is.
/// The drop line and the drop both use it, so these tests are what makes the line show where the item lands. A group's heading
/// and its shown rows are one item, split in halves; each item here is 20 high and the items touch.
/// The list the tests share, top to bottom: work (heading 0, rows A 20, B 40, C 60), home (heading 80, row D 100),
/// play (collapsed, heading 120), "No tag" (heading 140, row E 160), then "N resolved" (heading 180, rows R1 200, R2 220, ending at 240).
/// </summary>
[Unit]
public class DropTargetsTests
{
    private static GroupSpan Box(ref double top, string? tag, params string[] rows)
    {
        var start = top;
        top += 20;
        var boxes = new List<RowSpan>();
        foreach (var row in rows)
        {
            boxes.Add(new RowSpan(row, top, top + 20));
            top += 20;
        }

        return new GroupSpan(tag, start, top, boxes);
    }

    private static ListLayout Layout(bool withNoTag = true, bool withResolved = true)
    {
        var top = 0.0;
        var groups = new List<GroupSpan>
        {
            Box(ref top, "work", "A", "B", "C"),
            Box(ref top, "home", "D"),
            Box(ref top, "play"),
        };
        if (withNoTag)
        {
            groups.Add(Box(ref top, null, "E"));
        }

        if (!withResolved)
        {
            return new ListLayout(groups, null, []);
        }

        var resolved = Box(ref top, null, "R1", "R2");
        return new ListLayout(groups, resolved.Top, resolved.Rows);
    }

    private static TaskGap? OpenTask(string tag, string id, double y, ListLayout? layout = null) =>
        DropTargets.ForTask(layout ?? Layout(), tag, id, fromResolved: false, y);

    private static TaskGap? ResolvedTask(string id, double y) => DropTargets.ForTask(Layout(), null, id, fromResolved: true, y);

    private static int? Group(string? tag, double y, ListLayout? layout = null) => DropTargets.ForGroup(layout ?? Layout(), tag, y);

    // A task's gap

    [Fact]
    public void A_task_over_the_upper_half_of_a_row_of_its_group_lands_before_that_row()
    {
        Assert.Equal(new TaskGap("work", false, 0), OpenTask("work", "C", y: 22));
    }

    [Fact]
    public void A_task_over_the_lower_half_of_a_row_of_its_group_lands_after_that_row()
    {
        Assert.Equal(new TaskGap("work", false, 2), OpenTask("work", "A", y: 58));
    }

    [Fact]
    public void The_lower_half_of_one_row_and_the_upper_half_of_the_next_pick_the_same_gap()
    {
        Assert.Equal(OpenTask("work", "A", y: 58), OpenTask("work", "A", y: 62));
    }

    [Fact]
    public void A_task_over_the_lower_half_of_the_last_row_of_its_group_lands_at_the_end()
    {
        Assert.Equal(new TaskGap("work", false, 3), OpenTask("work", "A", y: 78));
    }

    [Theory]
    [InlineData(22)]
    [InlineData(38)]
    [InlineData(42)]
    public void A_task_has_no_gap_next_to_itself(double y)
    {
        Assert.Null(OpenTask("work", "A", y));
    }

    [Theory]
    [InlineData(10, "a group's heading")]
    [InlineData(90, "another group's heading")]
    [InlineData(110, "another group's row")]
    [InlineData(125, "a collapsed group's heading")]
    [InlineData(150, "the No tag heading")]
    [InlineData(170, "a No tag row")]
    public void A_task_has_no_gap_over_a_heading_or_another_groups_rows(double y, string over)
    {
        Assert.True(OpenTask("work", "A", y) is null, over);
    }

    [Theory]
    [InlineData(185)]
    [InlineData(205)]
    [InlineData(235)]
    public void An_open_task_has_no_gap_among_the_resolved_tasks(double y)
    {
        Assert.Null(OpenTask("work", "A", y));
    }

    [Fact]
    public void A_resolved_task_lands_among_the_resolved_tasks()
    {
        Assert.Equal(new TaskGap(null, true, 0), ResolvedTask("R2", y: 202));
    }

    [Fact]
    public void A_resolved_task_has_no_gap_next_to_itself()
    {
        Assert.Null(ResolvedTask("R1", y: 202));
    }

    [Fact]
    public void A_resolved_task_has_no_gap_among_the_open_tasks()
    {
        Assert.Null(ResolvedTask("R1", y: 50));
    }

    [Fact]
    public void A_task_of_the_No_tag_group_lands_among_that_groups_rows_only()
    {
        Assert.Null(OpenTask("work", "A", y: 170));
        Assert.Null(DropTargets.ForTask(Layout(), null, "E", fromResolved: false, y: 50));
    }

    [Fact]
    public void A_task_dragged_from_one_group_has_no_gap_in_another_group_that_shows_it_too()
    {
        Assert.Null(OpenTask("home", "D", y: 50));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(500)]
    public void A_task_has_no_gap_outside_the_lists_items(double y)
    {
        Assert.Null(OpenTask("work", "A", y));
    }

    // A group's gap

    [Fact]
    public void A_group_over_the_upper_half_of_another_group_lands_before_it()
    {
        Assert.Equal(0, Group("play", y: 10));
        Assert.Equal(1, Group("play", y: 85));
    }

    [Fact]
    public void A_group_over_the_lower_half_of_another_group_lands_after_it()
    {
        Assert.Equal(1, Group("play", y: 70));
        Assert.Equal(2, Group("work", y: 115));
    }

    [Fact]
    public void A_groups_heading_and_its_shown_rows_are_one_item_so_its_last_row_is_in_its_lower_half()
    {
        Assert.Equal(1, Group("play", y: 75));
    }

    [Fact]
    public void A_collapsed_group_is_its_heading_alone()
    {
        Assert.Equal(2, Group("work", y: 125));
        Assert.Equal(3, Group("work", y: 135));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(70)]
    public void A_group_has_no_gap_next_to_itself(double y)
    {
        Assert.Null(Group("work", y));
    }

    [Fact]
    public void A_group_has_no_gap_next_to_itself_after_it_too()
    {
        Assert.Null(Group("home", y: 115));
        Assert.Null(Group("home", y: 85));
    }

    [Fact]
    public void A_group_is_the_same_group_whatever_the_letter_case_of_its_tag()
    {
        Assert.Null(Group("WORK", y: 10));
    }

    [Theory]
    [InlineData(145)]
    [InlineData(155)]
    [InlineData(170)]
    [InlineData(178)]
    public void A_group_dropped_anywhere_over_No_tag_lands_just_before_it(double y)
    {
        Assert.Equal(3, Group("work", y));
    }

    [Fact]
    public void A_group_lands_after_the_last_group_when_there_is_no_No_tag_group()
    {
        var layout = Layout(withNoTag: false);

        Assert.Equal(3, Group("work", y: 135, layout));
    }

    [Theory]
    [InlineData(185)]
    [InlineData(210)]
    [InlineData(235)]
    public void A_group_has_no_gap_among_the_resolved_tasks(double y)
    {
        Assert.Null(Group("work", y));
    }

    [Fact]
    public void The_No_tag_group_cannot_be_dragged()
    {
        Assert.Null(Group(null, y: 10));
    }

    [Fact]
    public void A_group_picks_among_the_groups_the_list_shows_when_search_hides_others()
    {
        var top = 0.0;
        var shown = new ListLayout([Box(ref top, "work", "A"), Box(ref top, "play", "B")], null, []);

        Assert.Equal(0, Group("play", y: 10, shown));
        Assert.Equal(2, Group("work", y: 70, shown));
    }
}
