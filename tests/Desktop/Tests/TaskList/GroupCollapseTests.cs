using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Which tag groups are collapsed: every group starts expanded, a click on a tag's badge flips it, and a link reveals a hidden task.</summary>
[Unit]
public class GroupCollapseTests
{
    private readonly ListTasks _tasks = new();
    private readonly GroupCollapse _collapse = new();

    [Theory]
    [InlineData("home")]
    [InlineData(null)]
    public void Every_group_starts_expanded(string? tag)
    {
        Assert.False(_collapse.IsCollapsed(tag));
    }

    [Fact]
    public void Toggling_collapses_an_expanded_group_and_expands_it_again()
    {
        _collapse.Toggle("home");
        Assert.True(_collapse.IsCollapsed("home"));

        _collapse.Toggle("home");
        Assert.False(_collapse.IsCollapsed("home"));
    }

    [Fact]
    public void Collapsing_one_group_leaves_the_others_expanded()
    {
        _collapse.Toggle("home");

        Assert.False(_collapse.IsCollapsed("work"));
        Assert.False(_collapse.IsCollapsed(null));
    }

    [Fact]
    public void The_No_tag_group_collapses_on_its_own_and_is_not_a_tag_named_No_tag()
    {
        _collapse.Toggle(null);

        Assert.True(_collapse.IsCollapsed(null));
        Assert.False(_collapse.IsCollapsed("No tag"));
    }

    [Fact]
    public void A_tag_is_matched_ignoring_case()
    {
        _collapse.Toggle("Home");

        Assert.True(_collapse.IsCollapsed("home"));
        _collapse.Toggle("HOME");
        Assert.False(_collapse.IsCollapsed("Home"));
    }

    [Fact]
    public void Revealing_a_task_expands_its_collapsed_group()
    {
        var task = _tasks.Add("Buy milk", "", "home");
        _collapse.Toggle("home");

        _collapse.Reveal(task);

        Assert.False(_collapse.IsCollapsed("home"));
    }

    [Fact]
    public void Revealing_a_task_without_a_tag_expands_the_No_tag_group()
    {
        var task = _tasks.Add("Buy milk");
        _collapse.Toggle(null);

        _collapse.Reveal(task);

        Assert.False(_collapse.IsCollapsed(null));
    }

    [Fact]
    public void Revealing_a_task_leaves_the_other_collapsed_groups_collapsed()
    {
        var task = _tasks.Add("Buy milk", "", "home");
        _collapse.Toggle("home");
        _collapse.Toggle("work");
        _collapse.Toggle(null);

        _collapse.Reveal(task);

        Assert.True(_collapse.IsCollapsed("work"));
        Assert.True(_collapse.IsCollapsed(null));
    }

    [Fact]
    public void Revealing_a_task_that_already_shows_in_an_expanded_group_changes_nothing()
    {
        var task = _tasks.AddOlder("Buy milk", "", "home", "errand");
        _collapse.Toggle("home");

        _collapse.Reveal(task);

        Assert.True(_collapse.IsCollapsed("home"));
        Assert.False(_collapse.IsCollapsed("errand"));
    }

    [Fact]
    public void Revealing_a_task_whose_groups_are_all_collapsed_expands_only_the_group_of_its_first_row_in_group_order()
    {
        var task = _tasks.AddOlder("Buy milk", "", "work", "home", "errand");
        _collapse.Toggle("work");
        _collapse.Toggle("home");
        _collapse.Toggle("errand");

        _collapse.Reveal(task);

        Assert.False(_collapse.IsCollapsed("errand"));
        Assert.True(_collapse.IsCollapsed("home"));
        Assert.True(_collapse.IsCollapsed("work"));
    }

    [Fact]
    public void Group_order_for_a_reveal_ignores_the_case_of_the_tags()
    {
        var task = _tasks.AddOlder("Buy milk", "", "work", "Errand");
        _collapse.Toggle("work");
        _collapse.Toggle("Errand");

        _collapse.Reveal(task);

        Assert.False(_collapse.IsCollapsed("errand"));
        Assert.True(_collapse.IsCollapsed("work"));
    }

    [Fact]
    public void Revealing_a_task_in_an_expanded_group_changes_nothing()
    {
        var task = _tasks.Add("Buy milk", "", "home");

        _collapse.Reveal(task);

        Assert.False(_collapse.IsCollapsed("home"));
    }
}
