using System.IO;
using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Dragging a group's heading moves the group among the groups, and a drop line shows where a drag would land. These are the
/// common scenarios; the rule that picks the gap has its own tests (<see cref="DropTargetsTests"/>). The tests raise the standard WPF
/// drag events on a heading or a row, with data built the way a drag builds it, and check the groups the window shows, the
/// moves it asked Tasks' tag order for, and the drop line found by its automation id.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListGroupDragTests
{
    // Groups in the tag order's order: home (Home one), play (Play one), work (Work one).
    private static ListServices ThreeGroups()
    {
        var services = new ListServices();
        services.Add("Work one", "", "work");
        services.Add("Play one", "", "play");
        services.Add("Home one", "", "home");
        services.TagOrder.Seed("home", "play", "work");
        return services;
    }

    [WpfFact]
    public void Dropping_a_group_on_the_upper_half_of_another_group_puts_it_before_that_group()
    {
        var services = ThreeGroups();
        using var screen = new TaskListScreen(services);

        var taken = screen.DropGroup("work", "home");

        Assert.True(taken);
        Assert.Equal(["work", "home", "play"], screen.GroupNames);
        Assert.Equal([("work", (string?)null, (string?)"home")], services.TagOrder.Moves);
    }

    [WpfFact]
    public void Dropping_a_group_on_the_lower_half_of_another_group_puts_it_after_that_group()
    {
        var services = ThreeGroups();
        using var screen = new TaskListScreen(services);

        screen.DropGroup("home", "play", lowerHalf: true);

        Assert.Equal(["play", "home", "work"], screen.GroupNames);
        Assert.Equal([("home", (string?)"play", (string?)"work")], services.TagOrder.Moves);
    }

    [WpfFact]
    public void Dropping_a_group_over_No_tag_puts_it_just_before_No_tag()
    {
        var services = ThreeGroups();
        services.Add("Plain");
        using var screen = new TaskListScreen(services);

        screen.DropGroup("home", "No tag", lowerHalf: true);

        Assert.Equal(["play", "work", "home", "No tag"], screen.GroupNames);
    }

    [WpfFact]
    public void A_groups_move_saves_at_once_with_no_Undo_bar()
    {
        var services = ThreeGroups();
        using var screen = new TaskListScreen(services);

        screen.DropGroup("work", "home");

        Assert.False(screen.HasButton("Undo"));
        Assert.Single(services.TagOrder.Moves);
    }

    [WpfFact]
    public void A_groups_move_keeps_the_selection_and_a_collapsed_group_collapsed()
    {
        var services = ThreeGroups();
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Play one");
        screen.ToggleGroup("work");

        screen.DropGroup("work", "home");

        Assert.Equal(["Play one"], screen.SelectedTitles);
        Assert.Empty(screen.RowTitlesIn("work"));
    }

    [WpfFact]
    public void A_group_that_cannot_be_moved_goes_back_and_the_message_line_says_why()
    {
        var services = ThreeGroups();
        services.TagOrder.Failure = new InvalidDataException("tags.json has a newer version.");
        using var screen = new TaskListScreen(services);

        screen.DropGroup("work", "home");

        Assert.Equal(["home", "play", "work"], screen.GroupNames);
        Assert.True(screen.ShowsText("tags.json has a newer version."));
    }

    [WpfFact]
    public void Groups_reorder_when_the_tag_order_changes_elsewhere()
    {
        var services = ThreeGroups();
        using var screen = new TaskListScreen(services);

        services.TagOrder.Seed("work", "play", "home");
        services.TagOrder.RaiseChanged();
        screen.Settle();

        Assert.Equal(["work", "play", "home"], screen.GroupNames);
    }

    [WpfFact]
    public void A_group_dragged_over_another_group_shows_the_drop_line_just_above_its_heading()
    {
        using var screen = new TaskListScreen(ThreeGroups());

        var effects = screen.DragGroupOver("work", "play");

        Assert.NotEqual(DragDropEffects.None, effects);
        Assert.InRange(screen.DropLineTop!.Value, screen.HeadingTop("play") - 3, screen.HeadingTop("play") + 3);
    }

    [WpfFact]
    public void A_group_dragged_over_its_own_place_shows_no_line_and_the_no_drop_cursor()
    {
        using var screen = new TaskListScreen(ThreeGroups());

        var effects = screen.DragGroupOver("play", "play");

        Assert.Equal(DragDropEffects.None, effects);
        Assert.Null(screen.DropLineTop);
    }

    [WpfFact]
    public void The_drop_line_takes_no_room_so_the_headings_do_not_move()
    {
        using var screen = new TaskListScreen(ThreeGroups());
        var before = screen.HeadingTop("work");

        screen.DragGroupOver("home", "play");

        Assert.Equal(before, screen.HeadingTop("work"));
    }

    [WpfFact]
    public void The_drop_line_goes_away_when_the_group_is_dropped()
    {
        using var screen = new TaskListScreen(ThreeGroups());
        screen.DragGroupOver("work", "home");

        screen.DropGroup("work", "home");

        Assert.Null(screen.DropLineTop);
    }

    [WpfFact]
    public void A_task_dragged_over_a_gap_of_its_group_shows_the_drop_line_and_over_another_groups_row_shows_none()
    {
        var services = new ListServices();
        services.Add("C", "", "work");
        services.Add("B", "", "home");
        services.Add("A", "", "work");
        var task = services.Tasks.All.Single(t => t.Title == "A");
        using var screen = new TaskListScreen(services);

        var inside = screen.DragTaskOver(task, "C", lowerHalf: true, onGroup: "work");
        var lineInside = screen.DropLineTop;
        var other = screen.DragTaskOver(task, "B", onGroup: "home", fromGroup: "work");

        Assert.NotEqual(DragDropEffects.None, inside);
        Assert.NotNull(lineInside);
        Assert.Equal(DragDropEffects.None, other);
        Assert.Null(screen.DropLineTop);
    }
}
